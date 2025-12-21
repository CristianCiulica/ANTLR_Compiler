using ANTLR_Compiler;
using Antlr4.Runtime;
using System;
using System.IO;
using System.Linq;

class Program
{
    static void Main(string[] args)
    {
        string inputFile = "input.txt";
        if (!File.Exists(inputFile))
        {
            Console.WriteLine("Error: File 'input.txt' not found.");
            return;
        }

        string inputCode = File.ReadAllText(inputFile);

        AntlrInputStream inputStream = new AntlrInputStream(inputCode);
        MiniLangLexer lexer = new MiniLangLexer(inputStream);

        lexer.RemoveErrorListeners();
        SyntaxErrorListener errorListener = new SyntaxErrorListener();
        lexer.AddErrorListener(errorListener);

        CommonTokenStream tokenStream = new CommonTokenStream(lexer);
        tokenStream.Fill();

        using (StreamWriter sw = new StreamWriter("tokens.txt"))
        {
            foreach (var t in tokenStream.GetTokens())
            {
                if (t.Type == MiniLangLexer.Eof) continue;
                string typeName = lexer.Vocabulary.GetSymbolicName(t.Type) ?? "LITERAL";
                sw.WriteLine($"<{typeName}, {t.Text.Replace("\n", "\\n")}, {t.Line}>");
            }
        }

        tokenStream.Reset();
        MiniLangParser parser = new MiniLangParser(tokenStream);

        parser.RemoveErrorListeners();
        parser.AddErrorListener(errorListener);

        var tree = parser.program();

        AnalysisVisitor visitor = new AnalysisVisitor();
        visitor.Visit(tree);

        int mainCount = visitor.Functions.Count(f => f.IsMain);
        if (mainCount == 0)
        {
            visitor.SemanticErrors.Add("Error: No 'main' function found.");
        }
        else if (mainCount > 1)
        {
            visitor.SemanticErrors.Add("Error: Too many 'main' functions.");
        }

        using (StreamWriter sw = new StreamWriter("global_vars.txt"))
        {
            foreach (var v in visitor.GlobalVars)
                sw.WriteLine($"Variable: {v.Name} | Type: {v.Type} | Init: {v.InitValue}");
        }

        using (StreamWriter sw = new StreamWriter("functions.txt"))
        {
            foreach (var f in visitor.Functions)
            {
                string tipF = f.IsMain ? "MAIN" : (f.IsRecursive ? "RECURSIVE" : "ITERATIVE");
                sw.WriteLine($"Name: {f.Name}");
                sw.WriteLine($"   Type: {tipF}");
                sw.WriteLine($"   Returns: {f.Type}");

                string paramStr = string.Join(", ", f.Parameters.Select(p => $"{p.Type} {p.Name}"));
                sw.WriteLine($"   Params: [{paramStr}]");

                sw.WriteLine("   Local Vars:");
                if (f.LocalVars.Count == 0) sw.WriteLine("      (none)");
                foreach (var l in f.LocalVars)
                    sw.WriteLine($"      {l.Type} {l.Name} = {l.InitValue}");

                sw.WriteLine("   Control Structures:");
                if (f.ControlStructures.Count == 0) sw.WriteLine("      (none)");
                foreach (var c in f.ControlStructures)
                    sw.WriteLine($"      {c}");

                sw.WriteLine(new string('-', 20));
            }
        }

        using (StreamWriter sw = new StreamWriter("errors.txt"))
        {
            if (errorListener.Errors.Count == 0 && visitor.SemanticErrors.Count == 0)
            {
                sw.WriteLine("No errors found.");
            }
            else
            {
                if (errorListener.Errors.Count > 0)
                {
                    sw.WriteLine("=== Syntax/Lexical Errors ===");
                    foreach (var e in errorListener.Errors) sw.WriteLine(e);
                }

                if (visitor.SemanticErrors.Count > 0)
                {
                    sw.WriteLine("\n=== Semantic Errors ===");
                    foreach (var e in visitor.SemanticErrors) sw.WriteLine(e);
                }
            }
        }

        Console.WriteLine(" Complete. Check the generated txt files ");
        if (errorListener.Errors.Count > 0 || visitor.SemanticErrors.Count > 0)
            Console.WriteLine("Errors found");
    }
}