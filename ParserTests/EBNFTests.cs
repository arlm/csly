﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using expressionparser;
using jsonparser;
using jsonparser.JsonModel;
using simpleExpressionParser;
using sly.buildresult;
using sly.lexer;
using sly.parser;
using sly.parser.generator;
using sly.parser.llparser;
using sly.parser.parser;
using sly.parser.syntax;
using Xunit;

namespace ParserTests
{
    public enum OptionTestToken
    {
        [Lexeme("a")] a = 1,
        [Lexeme("b")] b = 2,
        [Lexeme("c")] c = 3,
        [Lexeme("e")] e = 4,
        [Lexeme("f")] f = 5,

        [Lexeme("[ \\t]+", true)] WS = 100,
        [Lexeme("\\n\\r]+", true, true)] EOF = 101
    }

    public enum GroupTestToken
    {
        [Lexeme("a")] A = 1,
        [Lexeme(",")] COMMA = 2,
        [Lexeme(";")] SEMICOLON = 3,


        [Lexeme("[ \\t]+", true)] WS = 100,
        [Lexeme("\\n\\r]+", true, true)] EOL = 101,
        EOF = 0
    }

    public class OptionTestParser
    {
        [Production("root2 : a B? c ")]
        public string root2(Token<OptionTestToken> a, ValueOption<string> b, Token<OptionTestToken> c)
        {
            var r = new StringBuilder();
            r.Append("R(");
            r.Append(a.Value);
            r.Append(b.Match(v => $",{v}", () => ",<none>"));
            r.Append($",{c.Value}");
            r.Append(")");
            return r.ToString();
        }

        [Production("root : a B c? ")]
        public string root(Token<OptionTestToken> a, string b, Token<OptionTestToken> c)
        {
            var r = $"R({a.StringWithoutQuotes},{b}";
            if (c.IsEmpty)
                r = $"{r},<none>)";
            else
                r = $"{r},{c.Value})";
            return r;
        }

        [Production("root : a b? c ")]
        public string root(Token<OptionTestToken> a, Token<OptionTestToken> b, Token<OptionTestToken> c)
        {
            var result = new StringBuilder();
            result.Append("R(");
            result.Append(a.StringWithoutQuotes);
            result.Append(",");
            if (b.IsEmpty)
                result.Append("<none>");
            else
                result.Append(b.StringWithoutQuotes);
            result.Append(",");
            result.Append(c.StringWithoutQuotes);
            result.Append(")");

            return result.ToString();
        }


        [Production("B : b ")]
        public string bee(Token<OptionTestToken> b)
        {
            return $"B({b.Value})";
        }
    }

    public class GroupTestParser
    {
        [Production("rootGroup : A ( COMMA A ) ")]
        public string root(Token<GroupTestToken> a, Group<GroupTestToken, string> group)
        {
            var r = new StringBuilder();
            r.Append("R(");
            r.Append(a.Value);
            r.Append("; {");
            group.Items.ForEach(item =>
            {
                r.Append(",");
                r.Append(item.IsValue ? item.Value : item.Token.Value);
            });
            r.Append("}");
            r.Append(")");
            return r.ToString();
        }

        [Production("rootMany : A ( COMMA [d] A )* ")]
        public string rootMany(Token<GroupTestToken> a, List<Group<GroupTestToken, string>> groups)
        {
            var r = new StringBuilder();
            r.Append("R(");
            r.Append(a.Value);
            groups.ForEach(group =>
            {
                group.Items.ForEach(item =>
                {
                    r.Append(",");
                    r.Append(item.Match(
                        (name, token) => token.Value,
                        (name, val) => val)
                    );
                });
            });
            r.Append(")");
            return r.ToString();
        }

        [Production("rootOption : A ( SEMICOLON [d] A )? ")]
        public string rootOption(Token<GroupTestToken> a, ValueOption<Group<GroupTestToken, string>> option)
        {
            var builder = new StringBuilder();
            builder.Append("R(");
            builder.Append(a.Value);
            var gg = option.Match(
                group =>
                {
                    var aToken = group.Token(0).Value;
                    builder.Append($";{aToken}");
                    return group;
                },
                () =>
                {
                    builder.Append(";");
                    builder.Append("a");
                    var g = new Group<GroupTestToken, string>();
                    g.Add("<none>", "<none>");
                    return g;
                });
            builder.Append(")");
            return builder.ToString();
        }

        [Production("root3 : A ( COMMA [d] A )* ")]
        public string root3(Token<GroupTestToken> a, List<Group<GroupTestToken, string>> groups)
        {
            var r = new StringBuilder();
            r.Append("R(");
            r.Append(a.Value);
            groups.ForEach(group =>
            {
                group.Items.ForEach(item =>
                {
                    r.Append(",");
                    r.Append(item.Value);
                });
            });
            return r.ToString();
        }
    }

    public class EBNFTests
    {
        public enum TokenType
        {
            [Lexeme("a")] a = 1,
            [Lexeme("b")] b = 2,
            [Lexeme("c")] c = 3,
            [Lexeme("e")] e = 4,
            [Lexeme("f")] f = 5,
            [Lexeme("[ \\t]+", true)] WS = 100,
            [Lexeme("\\n\\r]+", true, true)] EOL = 101
        }


        [Production("R : A B c ")]
        public string R(string A, string B, Token<TokenType> c)
        {
            var result = "R(";
            result += A + ",";
            result += B + ",";
            result += c.Value;
            result += ")";
            return result;
        }

        [Production("R : G+ ")]
        public string RManyNT(List<string> gs)
        {
            var result = "R(";
            result += gs
                .Select(g => g.ToString())
                .Aggregate((s1, s2) => s1 + "," + s2);
            result += ")";
            return result;
        }

        [Production("G : e f ")]
        public string RManyNT(Token<TokenType> e, Token<TokenType> f)
        {
            var result = $"G({e.Value},{f.Value})";
            return result;
        }

        [Production("A : a + ")]
        public string A(List<Token<TokenType>> astr)
        {
            var result = "A(";
            result += astr
                .Select(a => a.Value)
                .Aggregate((a1, a2) => a1 + ", " + a2);
            result += ")";
            return result;
        }

        [Production("B : b * ")]
        public string B(List<Token<TokenType>> bstr)
        {
            if (bstr.Any())
            {
                var result = "B(";
                result += bstr
                    .Select(b => b.Value)
                    .Aggregate((b1, b2) => b1 + ", " + b2);
                result += ")";
                return result;
            }

            return "B()";
        }


        private Parser<TokenType, string> Parser;

        private BuildResult<Parser<TokenType, string>> BuildParser()
        {
            var parserInstance = new EBNFTests();
            var builder = new ParserBuilder<TokenType, string>();
            var result = builder.BuildParser(parserInstance, ParserType.EBNF_LL_RECURSIVE_DESCENT, "R");
            return result;
        }


        private BuildResult<Parser<JsonToken, JSon>> BuildEbnfJsonParser()
        {
            var parserInstance = new EbnfJsonParser();
            var builder = new ParserBuilder<JsonToken, JSon>();

            var result =
                builder.BuildParser(parserInstance, ParserType.EBNF_LL_RECURSIVE_DESCENT, "root");
            return result;
        }

        private BuildResult<Parser<OptionTestToken, string>> BuildOptionParser()
        {
            var parserInstance = new OptionTestParser();
            var builder = new ParserBuilder<OptionTestToken, string>();

            var result =
                builder.BuildParser(parserInstance, ParserType.EBNF_LL_RECURSIVE_DESCENT, "root");
            return result;
        }

        private BuildResult<Parser<GroupTestToken, string>> BuildGroupParser()
        {
            var parserInstance = new GroupTestParser();
            var builder = new ParserBuilder<GroupTestToken, string>();

            var result =
                builder.BuildParser(parserInstance, ParserType.EBNF_LL_RECURSIVE_DESCENT, "rootGroup");
            return result;
        }

        private void AssertString(JObject obj, string key, string value)
        {
            Assert.True(obj.ContainsKey(key));
            Assert.True(obj[key].IsValue);
            var val = (JValue) obj[key];
            Assert.True(val.IsString);
            Assert.Equal(value, val.GetValue<string>());
        }

        private void AssertInt(JObject obj, string key, int value)
        {
            Assert.True(obj.ContainsKey(key));
            Assert.True(obj[key].IsValue);
            var val = (JValue) obj[key];
            Assert.True(val.IsInt);
            Assert.Equal(value, val.GetValue<int>());
        }


        private void AssertInt(JList list, int index, int value)
        {
            Assert.True(list[index].IsValue);
            var val = (JValue) list[index];
            Assert.True(val.IsInt);
            Assert.Equal(value, val.GetValue<int>());
        }

        [Fact]
        public void TestBuildGroupParser()
        {
            var buildResult = BuildGroupParser();
            Assert.False(buildResult.IsError);
            // var optionParser = buildResult.Result;

            // var result = optionParser.Parse("a , a", "root");
            // Assert.False(result.IsError);
            // Assert.Equal("R(a,B(b),c)", result.Result);
        }

        [Fact]
        public void TestEmptyOptionalNonTerminal()
        {
            var buildResult = BuildOptionParser();
            Assert.False(buildResult.IsError);
            var optionParser = buildResult.Result;

            var result = optionParser.Parse("a c", "root2");
            Assert.False(result.IsError);
            Assert.Equal("R(a,<none>,c)", result.Result);
        }

        [Fact]
        public void TestEmptyOptionTerminalInMiddle()
        {
            var buildResult = BuildOptionParser();
            Assert.False(buildResult.IsError);
            var optionParser = buildResult.Result;

            var result = optionParser.Parse("a c", "root");
            Assert.Equal("R(a,<none>,c)", result.Result);
        }


        [Fact]
        public void TestEmptyTerminalOption()
        {
            var buildResult = BuildOptionParser();
            Assert.False(buildResult.IsError);
            var optionParser = buildResult.Result;

            var result = optionParser.Parse("a b", "root");
            Assert.Equal("R(a,B(b),<none>)", result.Result);
        }

        [Fact]
        public void TestErrorMissingClosingBracket()
        {
            var jsonParser = new EbnfJsonGenericParser();
            var builder = new ParserBuilder<JsonTokenGeneric, JSon>();
            var parserTest = builder.BuildParser(jsonParser, ParserType.EBNF_LL_RECURSIVE_DESCENT, "root").Result;
            ParseResult<JsonTokenGeneric, JSon> r = null;
            try
            {
                r = parserTest.Parse("{");
            }
            catch (Exception e)
            {
                var stack = e.StackTrace;
                var message = e.Message;
                ;
            }

            Assert.True(r.IsError);
        }

        [Fact]
        public void TestGroupSyntaxManyParser()
        {
            var buildResult = BuildGroupParser();
            Assert.False(buildResult.IsError);
            var groupParser = buildResult.Result;
            var res = groupParser.Parse("a ,a , a ,a,a", "rootMany");

            Assert.False(res.IsError);
            Assert.Equal("R(a,a,a,a,a)", res.Result); // rootMany
        }

        [Fact]
        public void TestGroupSyntaxOptionParser()
        {
            var buildResult = BuildGroupParser();
            Assert.False(buildResult.IsError);
            var groupParser = buildResult.Result;
            var res = groupParser.Parse("a ; a ", "rootOption");

            Assert.False(res.IsError);
            Assert.Equal("R(a;a)", res.Result); // rootMany
        }

        [Fact]
        public void TestGroupSyntaxParser()
        {
            var buildResult = BuildGroupParser();
            Assert.False(buildResult.IsError);
            var groupParser = buildResult.Result;
            var res = groupParser.Parse("a ,a");

            Assert.False(res.IsError);
            Assert.Equal("R(a; {,,,a})", res.Result);
        }


        [Fact]
        public void TestJsonList()
        {
            var buildResult = BuildEbnfJsonParser();
            Assert.False(buildResult.IsError);
            var jsonParser = buildResult.Result;

            var result = jsonParser.Parse("[1,2,3,4]");
            Assert.False(result.IsError);
            Assert.True(result.Result.IsList);
            var list = (JList) result.Result;
            Assert.Equal(4, list.Count);
            AssertInt(list, 0, 1);
            AssertInt(list, 1, 2);
            AssertInt(list, 2, 3);
            AssertInt(list, 3, 4);
        }

        [Fact]
        public void TestJsonObject()
        {
            var buildResult = BuildEbnfJsonParser();
            Assert.False(buildResult.IsError);
            var jsonParser = buildResult.Result;
            var result = jsonParser.Parse("{\"one\":1,\"two\":2,\"three\":\"trois\" }");
            Assert.False(result.IsError);
            Assert.True(result.Result.IsObject);
            var o = (JObject) result.Result;
            Assert.Equal(3, o.Count);
            AssertInt(o, "one", 1);
            AssertInt(o, "two", 2);
            AssertString(o, "three", "trois");
        }

        [Fact]
        public void TestNonEmptyOptionalNonTerminal()
        {
            var buildResult = BuildOptionParser();
            Assert.False(buildResult.IsError);
            var optionParser = buildResult.Result;

            var result = optionParser.Parse("a b c", "root2");
            Assert.False(result.IsError);
            Assert.Equal("R(a,B(b),c)", result.Result);
        }


        [Fact]
        public void TestNonEmptyTerminalOption()
        {
            var buildResult = BuildOptionParser();
            Assert.False(buildResult.IsError);
            var optionParser = buildResult.Result;

            var result = optionParser.Parse("a b c", "root");
            Assert.Equal("R(a,B(b),c)", result.Result);
        }


        [Fact]
        public void TestOneOrMoreNonTerminal()
        {
            var buildResult = BuildParser();
            Assert.False(buildResult.IsError);
            Parser = buildResult.Result;
            var result = Parser.Parse("e f e f");
            Assert.False(result.IsError);
            Assert.Equal("R(G(e,f),G(e,f))", result.Result.Replace(" ", ""));
        }


        [Fact]
        public void TestOneOrMoreWithMany()
        {
            var buildResult = BuildParser();
            Assert.False(buildResult.IsError);
            Parser = buildResult.Result;
            var result = Parser.Parse("aaa b c");
            Assert.False(result.IsError);
            Assert.Equal("R(A(a,a,a),B(b),c)", result.Result.Replace(" ", ""));
        }

        [Fact]
        public void TestOneOrMoreWithOne()
        {
            var buildResult = BuildParser();
            Assert.False(buildResult.IsError);
            Parser = buildResult.Result;
            var result = Parser.Parse(" b c");
            Assert.True(result.IsError);
        }

        [Fact]
        public void TestParseBuild()
        {
            var buildResult = BuildParser();
            Assert.False(buildResult.IsError);
            Parser = buildResult.Result;
            Assert.Equal(typeof(EBNFRecursiveDescentSyntaxParser<TokenType, string>), Parser.SyntaxParser.GetType());
            Assert.Equal(4, Parser.Configuration.NonTerminals.Count);
            var nt = Parser.Configuration.NonTerminals["R"];
            Assert.Equal(2, nt.Rules.Count);
            nt = Parser.Configuration.NonTerminals["A"];
            Assert.Single(nt.Rules);
            var rule = nt.Rules[0];
            Assert.Single(rule.Clauses);
            Assert.IsType<OneOrMoreClause<TokenType>>(rule.Clauses[0]);
            nt = Parser.Configuration.NonTerminals["B"];
            Assert.Single(nt.Rules);
            rule = nt.Rules[0];
            Assert.Single(rule.Clauses);
            Assert.IsType<ZeroOrMoreClause<TokenType>>(rule.Clauses[0]);
        }

        [Fact]
        public void TestZeroOrMoreWithMany()
        {
            var buildResult = BuildParser();
            Assert.False(buildResult.IsError);
            Parser = buildResult.Result;
            var result = Parser.Parse("a bb c");
            Assert.False(result.IsError);
            Assert.Equal("R(A(a),B(b,b),c)", result.Result.Replace(" ", ""));
        }

        [Fact]
        public void TestZeroOrMoreWithNone()
        {
            var buildResult = BuildParser();
            Assert.False(buildResult.IsError);
            Parser = buildResult.Result;
            var result = Parser.Parse("a  c");
            Assert.False(result.IsError);
            Assert.Equal("R(A(a),B(),c)", result.Result.Replace(" ", ""));
        }

        [Fact]
        public void TestZeroOrMoreWithOne()
        {
            var buildResult = BuildParser();
            Assert.False(buildResult.IsError);
            Parser = buildResult.Result;
            var result = Parser.Parse("a b c");
            Assert.False(result.IsError);
            Assert.Equal("R(A(a),B(b),c)", result.Result.Replace(" ", ""));
        }
        
        
        
        #region CONTEXTS
        
        private BuildResult<Parser<ExpressionToken, int>> buildSimpleExpressionParserWithContext()
        {
            var startingRule = $"{typeof(SimpleExpressionParserWithContext).Name}_expressions";
            var parserInstance = new SimpleExpressionParserWithContext();
            var builder = new ParserBuilder<ExpressionToken, int>();
            var parser = builder.BuildParser(parserInstance, ParserType.LL_RECURSIVE_DESCENT, startingRule);
            return parser;
        }

        [Fact]
        public void TestContextualParsing()
        {
            var buildResult = buildSimpleExpressionParserWithContext();
            
            Assert.False(buildResult.IsError);
            var parser = buildResult.Result;
            var res = parser.ParseWithContext("2 + a", new Dictionary<string, int> {{"a", 2}});
            Assert.True(res.IsOk);
            Assert.Equal(4,res.Result);
        }

        [Fact]
        public void TestContextualParsing2()
        {
            var buildResult = buildSimpleExpressionParserWithContext();
            
            Assert.False(buildResult.IsError);
            var parser = buildResult.Result;
            var res = parser.ParseWithContext("2 + a * b", new Dictionary<string, int> {{"a", 2},{"b",3}});
            Assert.True(res.IsOk);
            Assert.Equal(8,res.Result);
        }
        
        
        #endregion
        
    }
}