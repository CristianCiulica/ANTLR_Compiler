using Antlr4.Runtime;
using System.Collections.Generic;
using System.IO;

public class SyntaxErrorListener : BaseErrorListener, IAntlrErrorListener<int>
{
    public List<string> Errors = new List<string>();
    public override void SyntaxError(IRecognizer recognizer, IToken offendingSymbol, int line, int charPositionInLine, string msg, RecognitionException e)
    {
        Errors.Add($"Eroare Sintactica la linia {line}:{charPositionInLine} - {msg}");
    }
    public void SyntaxError(IRecognizer recognizer, int offendingSymbol, int line, int charPositionInLine, string msg, RecognitionException e)
    {
        Errors.Add($"Eroare Lexicala la linia {line}:{charPositionInLine} - {msg}");
    }
}