using Antlr4.Runtime;
using System.Collections.Generic;

namespace ANTLR_Compiler
{
    public class SyntaxErrorListener : BaseErrorListener, IAntlrErrorListener<int>
    {
        public List<string> Errors = new List<string>();

        public override void SyntaxError(IRecognizer recognizer, IToken offendingSymbol, int line, int charPositionInLine, string msg, RecognitionException e)
        {
            Errors.Add($"Syntax error at line {line}:{charPositionInLine} - {msg}");
        }

        public void SyntaxError(IRecognizer recognizer, int offendingSymbol, int line, int charPositionInLine, string msg, RecognitionException e)
        {
            Errors.Add($"Lexical error at line {line}:{charPositionInLine} - {msg}");
        }
    }
}