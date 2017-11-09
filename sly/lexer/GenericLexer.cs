﻿using sly.lexer.fsm;
using System;
using System.Collections.Generic;
using System.Text;

namespace sly.lexer
{

    public enum GenericToken
    {
        Identifier,
        Int,
        Double,
        KeyWord,
        String,
        SugarToken
    }

    public enum EOLType
    {
        Windows,
        Nix,
        Environment
    }

    public class GenericLexer<IN> : ILexer<IN> where IN : struct
    {
        private const string in_string = "in_string";
        private const string string_end = "string_end";
        private const string start = "start";
        private const string in_int = "in_int";
        private const string start_double = "start_double";
        private const string in_double = "in_double";
        private const string in_identifier = "in_identifier";
        private const string token_property = "token";
        public const string DerivedToken = "derivedToken";
        private FSMLexer<GenericToken, GenericToken> LexerFsm;

        private Dictionary<GenericToken, Dictionary<string, IN>> derivedTokens;
        private IN identifierDerivedToken;
        private FSMLexerBuilder<GenericToken, GenericToken> FSMBuilder;


        public GenericLexer(EOLType eolType)
        {
            InitializeStaticLexer(eolType);
            derivedTokens = new Dictionary<GenericToken, Dictionary<string, IN>>();
        }

        private void InitializeStaticLexer(EOLType eolType)
        {
            FSMBuilder = new FSMLexerBuilder<GenericToken, GenericToken>();


            // conf
            FSMBuilder.IgnoreWS()
                .WhiteSpace(' ')
                .WhiteSpace('\t')
                .IgnoreEOL();
            switch (eolType)
            {
                case EOLType.Windows:
                    {
                        FSMBuilder.UseWindowsEOL();
                        break;
                    }
                case EOLType.Nix:
                    {
                        FSMBuilder.UseNixEOL();
                        break;
                    }
                case EOLType.Environment:
                    {
                        FSMBuilder.UseEnvironmentEOL();
                        break;
                    }
            }


            // start machine definition
            FSMBuilder.Mark(start);



            // string literal
            FSMBuilder.Transition('\"', GenericToken.String)
                .Mark(in_string)
                .ExceptTransitionTo(new char[] { '\"', '\\' }, "in_string", GenericToken.String)
                .Transition('\\', GenericToken.String)
                .Mark("escape")
                .AnyTransitionTo(' ', "in_string", GenericToken.String)
                .Transition('\"', GenericToken.String)
                .End(GenericToken.String)
                .Mark(string_end);

            // identifier
            FSMBuilder.GoTo(start).
            RangeTransition('a', 'z', GenericToken.Identifier).
            Mark(in_identifier)
            .End(GenericToken.Identifier);

            FSMBuilder.GoTo(start).
            RangeTransitionTo('A', 'Z', in_identifier, GenericToken.Identifier, GenericToken.Identifier).
            RangeTransitionTo('a', 'z', in_identifier,GenericToken.Identifier).
            RangeTransitionTo('A', 'Z', in_identifier, GenericToken.Identifier).
            End(GenericToken.Identifier);


            //numeric
            FSMBuilder.GoTo(start)
            .RangeTransition('0', '9', GenericToken.Int, GenericToken.Double)
            .Mark(in_int)
            .RangeTransitionTo('0', '9', in_int, GenericToken.Int, GenericToken.Double)
            .End(GenericToken.Int)
            .Transition('.', GenericToken.Double)
            .Mark(start_double)
            .RangeTransition('0', '9', GenericToken.Int, GenericToken.Int, GenericToken.Double)
            .Mark(in_double)
            .RangeTransitionTo('0', '9', in_double, GenericToken.Int, GenericToken.Double)
            .End(GenericToken.Double);


            string code = "{\"d\" : 42.42 , \"i\" : 42 , \"s\" : \"quarante-deux\" }";
            LexerFsm = FSMBuilder.Fsm;
        }


        public void AddLexeme(GenericToken generic, IN token)
        {
            if (generic == GenericToken.Identifier)
            {
                identifierDerivedToken = token;
            }

            NodeCallback<GenericToken> callback = (FSMMatch<GenericToken> match) =>
            {
                if (match.Result.TokenID == GenericToken.Identifier)
                {
                    match.Properties[DerivedToken] = identifierDerivedToken;
                }
                else
                {
                    match.Properties[DerivedToken] = token;
                }

                return match;
            };

            switch (generic)
            {
                case GenericToken.String:
                    {
                        FSMBuilder.GoTo(string_end);
                        break;
                    }
                case GenericToken.Double:
                    {
                        FSMBuilder.GoTo(in_double);
                        break;
                    }
                case GenericToken.Int:
                    {
                        FSMBuilder.GoTo(in_int);
                        break;
                    }
                case GenericToken.Identifier:
                    {
                        FSMBuilder.GoTo(in_identifier);
                        break;
                    }
            }

            FSMBuilder.CallBack(callback);

        }

        public void AddLexeme(GenericToken genericToken, IN token, string specialValue)
        {
            switch (genericToken)
            {
                case GenericToken.SugarToken:
                    {
                        AddSugarLexem(token, specialValue);
                        break;
                    }                    
            }
            Dictionary<string, IN> tokensForGeneric = new Dictionary<string, IN>();
            if (derivedTokens.ContainsKey(genericToken))
            {
                tokensForGeneric = derivedTokens[genericToken];
            }
            tokensForGeneric[specialValue] = token;
            
        }

        public void AddKeyWord(IN token, string keyword)
        {
            NodeCallback<GenericToken> callback = (FSMMatch<GenericToken> match) =>
            {
                if (derivedTokens.ContainsKey(GenericToken.Identifier))
                {
                    Dictionary<string, IN> derived = derivedTokens[GenericToken.Identifier];
                    if (derived.ContainsKey(keyword))
                    {
                        match.Properties[token_property] = token;
                    }
                }
                return match;
            };

            AddLexeme(GenericToken.Identifier, token, keyword);
            FSMBuilder.GoTo(in_identifier);
            var node = FSMBuilder.GetNode(in_identifier);
            if (!FSMBuilder.Fsm.HasCallback(node.Id))
            {
                FSMBuilder.GoTo(in_identifier).
                    CallBack(callback);
            }
        }


        public void AddSugarLexem(IN token, string specialValue)
        {
            NodeCallback<GenericToken> callback = (FSMMatch<GenericToken> match) =>
            {
                match.Properties[DerivedToken] = token;
                return match;
            };

            FSMBuilder.GoTo(start);
            for (int i = 0; i < specialValue.Length; i++)
            {
                FSMBuilder.SafeTransition(specialValue[i], GenericToken.SugarToken);                
            }
            FSMBuilder.End(GenericToken.SugarToken)
                .CallBack(callback);

        }


        public void AddDefinition(TokenDefinition<IN> tokenDefinition)
        {

        }

        public IEnumerable<Token<IN>> Tokenize(string source)
        {
            List<Token<IN>> tokens = new List<Token<IN>>();
            var r = LexerFsm.Run(source, 0);
            while (r.IsSuccess)
            {
                tokens.Add(Transcode(r));// r.Result;
                //Console.WriteLine($"{r.Result.TokenID} : {r.Result.Value} @{r.Result.Position}");
                r = LexerFsm.Run(source);
                
            }
            return tokens;

        }

        public Token<IN> Transcode(FSMMatch<GenericToken> match)
        {
            var tok = new Token<IN>();
            tok.Value = match.Result.Value;
            tok.Position = match.Result.Position;
            tok.TokenID = (IN)match.Properties[DerivedToken];
            return tok;
        }
    }
}
