using ANTLR_Compiler;
using Antlr4.Runtime.Misc;
using System.Collections.Generic;
using System.Linq;

public class AnalysisVisitor : MiniLangBaseVisitor<string>
{
    public List<Symbol> GlobalVars = new List<Symbol>();
    public List<FunctionSymbol> Functions = new List<FunctionSymbol>();
    public List<string> SemanticErrors = new List<string>();

    private Stack<Dictionary<string, Symbol>> scopes = new Stack<Dictionary<string, Symbol>>();
    private FunctionSymbol currentFunction = null;

    public AnalysisVisitor()
    {
        scopes.Push(new Dictionary<string, Symbol>());
    }

    private void AddError(int line, string msg)
    {
        string err = $"Eroare Semantica la linia {line}: {msg}";
        if (!SemanticErrors.Contains(err)) SemanticErrors.Add(err);
    }

    private Symbol Resolve(string name)
    {
        foreach (var scope in scopes)
        {
            if (scope.ContainsKey(name)) return scope[name];
        }
        return null;
    }

    private bool IsCompatible(string targetType, string valueType)
    {
        if (valueType == "unknown" || valueType == null) return false;
        if (valueType == "null") return true;
        if (targetType == valueType) return true;
        if (targetType == "double" && (valueType == "float" || valueType == "int")) return true;
        if (targetType == "float" && valueType == "int") return true;
        return false;
    }

    public override string VisitGlobalVarDeclaration(MiniLangParser.GlobalVarDeclarationContext context)
    {
        ProcessVarDecl(context.varDecl(), isGlobal: true);
        return null;
    }

    public override string VisitLocalVarStmt(MiniLangParser.LocalVarStmtContext context)
    {
        ProcessVarDecl(context.varDecl(), isGlobal: false);
        return null;
    }

    private void ProcessVarDecl(MiniLangParser.VarDeclContext ctx, bool isGlobal)
    {
        string type = ctx.type().GetText();
        string name = ctx.IDENTIFIER().GetText();
        bool isConst = ctx.CONST() != null;
        string initVal = "null";
        string valType = null;

        if (ctx.expression() != null)
        {
            initVal = ctx.expression().GetText();
            valType = Visit(ctx.expression());
        }

        if (scopes.Peek().ContainsKey(name))
        {
            AddError(ctx.Start.Line, $"Variabila '{(isGlobal ? "globala" : "locala")}' '{name}' este deja definita.");
        }
        else
        {
            if (valType != null && !IsCompatible(type, valType))
                AddError(ctx.Start.Line, $"Incompatibilitate tip '{name}'. Așteptat: {type}, Primit: {valType}");

            var sym = new Symbol { Name = name, Type = type, IsConst = isConst, InitValue = initVal };
            scopes.Peek()[name] = sym;

            if (isGlobal) GlobalVars.Add(sym);
            else if (currentFunction != null) currentFunction.LocalVars.Add(sym);
        }
    }

    public override string VisitGlobalFuncDeclaration(MiniLangParser.GlobalFuncDeclarationContext context)
    {
        var funcCtx = context.funcDecl();
        string retType = "void";
        if (funcCtx.type() != null) retType = funcCtx.type().GetText();

        string name = funcCtx.IDENTIFIER().GetText();

        if (Functions.Any(f => f.Name == name))
        {
            AddError(context.Start.Line, $"Functia '{name}' este deja definita.");
        }

        currentFunction = new FunctionSymbol(name, retType);
        if (name == "main") currentFunction.IsMain = true;
        Functions.Add(currentFunction);

        scopes.Push(new Dictionary<string, Symbol>());

        if (funcCtx.paramList() != null)
        {
            foreach (var paramCtx in funcCtx.paramList().param())
            {
                string pType = paramCtx.type().GetText();
                string pName = paramCtx.IDENTIFIER().GetText();
                var pSym = new Symbol { Name = pName, Type = pType, IsConst = false };

                currentFunction.Parameters.Add(pSym);

                if (scopes.Peek().ContainsKey(pName))
                    AddError(paramCtx.Start.Line, $"Parametrul '{pName}' este duplicat.");
                else
                    scopes.Peek()[pName] = pSym;
            }
        }

        Visit(funcCtx.block());

        if (retType != "void" && !currentFunction.HasReturn)
            AddError(context.Start.Line, $"Functia '{name}' (tip {retType}) nu returneaza o valoare.");

        scopes.Pop();
        currentFunction = null;
        return null;
    }

    public override string VisitIfStmt(MiniLangParser.IfStmtContext context)
    {
        if (currentFunction != null)
            currentFunction.ControlStructures.Add($"<if, {context.Start.Line}, {context.Stop.Line}>");
        Visit(context.expression());
        Visit(context.block(0));
        if (context.block().Length > 1) Visit(context.block(1));
        return null;
    }

    public override string VisitWhileStmt(MiniLangParser.WhileStmtContext context)
    {
        if (currentFunction != null)
            currentFunction.ControlStructures.Add($"<while, {context.Start.Line}, {context.Stop.Line}>");
        Visit(context.expression());
        Visit(context.block());
        return null;
    }

    public override string VisitForStmt(MiniLangParser.ForStmtContext context)
    {
        if (currentFunction != null)
            currentFunction.ControlStructures.Add($"<for, {context.Start.Line}, {context.Stop.Line}>");

        scopes.Push(new Dictionary<string, Symbol>());
        base.VisitForStmt(context);
        scopes.Pop();
        return null;
    }

    public override string VisitReturnStmt(MiniLangParser.ReturnStmtContext context)
    {
        if (currentFunction == null) return null;

        currentFunction.HasReturn = true;
        string exprType = "void";
        if (context.expression() != null)
            exprType = Visit(context.expression());

        if (!IsCompatible(currentFunction.Type, exprType))
            AddError(context.Start.Line, $"Return invalid. Functia '{currentFunction.Name}' cere {currentFunction.Type}, returnat {exprType}");

        return null;
    }

    public override string VisitParenExpr(MiniLangParser.ParenExprContext context)
    {
        return Visit(context.expression());
    }

    public override string VisitAssignmentExpr(MiniLangParser.AssignmentExprContext context)
    {
        return Visit(context.assignStmt());
    }

    public override string VisitAssignStmt(MiniLangParser.AssignStmtContext context)
    {
        string varName = context.IDENTIFIER().GetText();
        Symbol sym = Resolve(varName);

        if (sym == null)
        {
            AddError(context.Start.Line, $"Variabila '{varName}' nedeclarata.");
            return "unknown";
        }
        if (sym.IsConst)
            AddError(context.Start.Line, $"Atribuire ilegala la constanta '{varName}'.");

        if (context.expression() != null)
        {
            string exprType = Visit(context.expression());
            if (!IsCompatible(sym.Type, exprType))
                AddError(context.Start.Line, $"Nu se poate atribui {exprType} la {sym.Type}.");
        }
        return sym.Type;
    }

    public override string VisitAddSubExpr(MiniLangParser.AddSubExprContext context)
    {
        string left = Visit(context.expression(0));
        string right = Visit(context.expression(1));
        if (left == "string" || right == "string") return "string";
        if (left == "float" || right == "float" || left == "double" || right == "double") return "float";
        if (left == "int" && right == "int") return "int";
        return "unknown";
    }

    public override string VisitMulDivExpr(MiniLangParser.MulDivExprContext context)
    {
        string left = Visit(context.expression(0));
        string right = Visit(context.expression(1));
        if (left == "float" || right == "float" || left == "double" || right == "double") return "float";
        if (left == "int" && right == "int") return "int";
        return "unknown";
    }

    public override string VisitRelationalExpr(MiniLangParser.RelationalExprContext context)
    {
        Visit(context.expression(0));
        Visit(context.expression(1));
        return "int";
    }

    public override string VisitEqualityExpr(MiniLangParser.EqualityExprContext context)
    {
        Visit(context.expression(0));
        Visit(context.expression(1));
        return "int";
    }

    public override string VisitLogicAndExpr(MiniLangParser.LogicAndExprContext context) => CheckLogic(context.expression(0), context.expression(1));
    public override string VisitLogicOrExpr(MiniLangParser.LogicOrExprContext context) => CheckLogic(context.expression(0), context.expression(1));

    private string CheckLogic(MiniLangParser.ExpressionContext e1, MiniLangParser.ExpressionContext e2)
    {
        Visit(e1); Visit(e2);
        return "int";
    }

    public override string VisitNotExpr(MiniLangParser.NotExprContext context)
    {
        Visit(context.expression());
        return "int";
    }

    public override string VisitFuncCallExpr(MiniLangParser.FuncCallExprContext context)
    {
        string funcName = context.IDENTIFIER().GetText();
        var targetFunc = Functions.FirstOrDefault(f => f.Name == funcName);

        if (targetFunc == null)
        {
            AddError(context.Start.Line, $"Apel functie nedefinita: '{funcName}'.");
            return "unknown";
        }

        if (targetFunc.IsMain)
            AddError(context.Start.Line, "Functia 'main' nu poate fi apelata.");

        if (currentFunction != null && currentFunction.Name == funcName)
            currentFunction.IsRecursive = true;

        var args = context.args() != null ? context.args().expression() : new MiniLangParser.ExpressionContext[0];

        if (args.Length != targetFunc.Parameters.Count)
        {
            AddError(context.Start.Line, $"Numar argumente incorect la '{funcName}'. Asteptat {targetFunc.Parameters.Count}, Primit {args.Length}");
        }
        else
        {
            for (int i = 0; i < args.Length; i++)
            {
                string argType = Visit(args[i]);
                string paramType = targetFunc.Parameters[i].Type;
                if (!IsCompatible(paramType, argType))
                    AddError(context.Start.Line, $"Argument {i + 1} incompatibil la '{funcName}'. Asteptat {paramType}, Primit {argType}");
            }
        }
        return targetFunc.Type;
    }

    public override string VisitIdExpr(MiniLangParser.IdExprContext context)
    {
        string name = context.IDENTIFIER().GetText();
        Symbol sym = Resolve(name);
        if (sym == null)
        {
            AddError(context.Start.Line, $"Variabila '{name}' nu este declarata.");
            return "unknown";
        }
        return sym.Type;
    }

    public override string VisitIntExpr(MiniLangParser.IntExprContext context) => "int";
    public override string VisitFloatExpr(MiniLangParser.FloatExprContext context) => "float";
    public override string VisitStringExpr(MiniLangParser.StringExprContext context) => "string";
}