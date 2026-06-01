using Antlr4.Runtime;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Mini_GO_compiler.SyntaxChecker
{
    public class MiniGoLexerErrorListener : IAntlrErrorListener<int>
    {
        private LinkedList<string> errorList = new LinkedList<string>();

        public LinkedList<string> ErrorList
        {
            get { return errorList; }
        }

        public bool HasErrors()
        {
            return errorList.Any();
        }

        public void SyntaxError(TextWriter output, IRecognizer recognizer, int offendingSymbol, int line, int charPositionInLine, string msg, RecognitionException e)
        {
            errorList.AddLast($"LEXER ERROR: {msg} en [line {line}:{charPositionInLine}]");
        }
    }
}
