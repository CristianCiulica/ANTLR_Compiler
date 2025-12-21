using System.Collections.Generic;

namespace ANTLR_Compiler
{
    public class Symbol
    {
        public string Name { get; set; }
        public string Type { get; set; }
        public bool IsConst { get; set; }
        public string InitValue { get; set; }
    }

    public class FunctionSymbol : Symbol
    {
        public List<Symbol> Parameters { get; set; } = new List<Symbol>();
        public List<Symbol> LocalVars { get; set; } = new List<Symbol>();
        public List<string> ControlStructures { get; set; } = new List<string>();
        public bool IsRecursive { get; set; } = false;
        public bool HasReturn { get; set; } = false;
        public bool IsMain { get; set; } = false;

        public FunctionSymbol(string name, string type)
        {
            Name = name;
            Type = type;
            IsConst = false;
        }
    }
}