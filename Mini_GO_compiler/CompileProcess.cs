using SyntaxChecker;
using System.IO;
using Antlr4.Runtime;
using Antlr4.Runtime.Tree;
using Mini_GO_compiler.SyntaxChecker;
using SyntaxChecker.generated;
using Mini_GO_compiler.TypeChecker;
using Mini_GO_compiler.Encoder;

namespace Mini_GO_compiler;

public static class CompileProcess
{
    public static LinkedList<string> PreCompile(string file)
    {
            var inputStream = new AntlrInputStream(file);
            var lexer = new MiniGoCompilerLexer(inputStream);

            MiniGoLexerErrorListener lexerErrorListener = new MiniGoLexerErrorListener();
            lexer.RemoveErrorListeners();
            lexer.AddErrorListener(lexerErrorListener);

            var tokenStream = new CommonTokenStream(lexer);
            var parser = new MiniGoCompilerParser(tokenStream);

            MiniGoErrorListener parserErrorListener = new MiniGoErrorListener();
            parser.RemoveErrorListeners();
            parser.AddErrorListener(parserErrorListener);
            
            var tree = parser.root();

            if (lexerErrorListener.HasErrors())
            {
                return lexerErrorListener.ErrorList;
            }
            else if (parserErrorListener.HasParserErrors())
            {
                return parserErrorListener.ErrorParserList;
            }
            else if (tokenStream.LT(1).Type != TokenConstants.EOF)
            {
                IToken remainingToken = tokenStream.LT(1);
                LinkedList<string> list = new LinkedList<string>();
                list.AddLast($"PARSER ERROR: token inesperado '{remainingToken.Text}' en [line {remainingToken.Line}:{remainingToken.Column}]");
                return list;
            }
            else if (parser.NumberOfSyntaxErrors > 0)
            {
                // Si hay errores de sintaxis pero no fueron capturados por el listener
                LinkedList<string> list = new LinkedList<string>();
                list.AddLast($"PARSER ERROR: Se encontraron {parser.NumberOfSyntaxErrors} errores de sintaxis no capturados");
                return list;
            }
            else
            {
                // Análisis Semántico (Type Checker)
                var typeChecker = new TypeCheckerVisitor();
                typeChecker.Visit(tree);

                if (typeChecker.HasErrors())
                {
                    return typeChecker.ErrorList;
                }
                else
                {
                    //EncoderVisitor encoderVisitor = new EncoderVisitor();
                    //encoderVisitor.Visit(tree);
                    LinkedList<string> list = new LinkedList<string>();
                    return list;
                }
            }
    }   
}