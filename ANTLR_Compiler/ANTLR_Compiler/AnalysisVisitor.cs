using Antlr4.Runtime.Misc;
using System.Collections.Generic;
using System.Linq;

namespace ANTLR_Compiler
{
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
            string err = $"Error at line {line}: {msg}";
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

        public override string VisitProgram(MiniLangParser.ProgramContext context)
        {
            foreach (var globalDecl in context.globalDecl())
            {
                if (globalDecl is MiniLangParser.GlobalFuncDeclarationContext funcWrapper)
                {
                    PreProcessFunction(funcWrapper.funcDecl(), globalDecl.Start.Line);
                }
            }
            return base.VisitProgram(context);
        }

        private void PreProcessFunction(MiniLangParser.FuncDeclContext ctx, int line)
        {
            string retType = "void";
            if (ctx.type() != null) retType = ctx.type().GetText();
            string name = ctx.IDENTIFIER().GetText();

            if (Functions.Any(f => f.Name == name))
            {
                AddError(line, $"Function '{name}' is defined twice.");
                return;
            }

            var funcSym = new FunctionSymbol(name, retType);
            if (name == "main") funcSym.IsMain = true;

            if (ctx.paramList() != null)
            {
                foreach (var paramCtx in ctx.paramList().param())
                {
                    string pType = paramCtx.type().GetText();
                    string pName = paramCtx.IDENTIFIER().GetText();
                    funcSym.Parameters.Add(new Symbol { Name = pName, Type = pType });
                }
            }
            Functions.Add(funcSym);
        }

        public override string VisitGlobalFuncDeclaration(MiniLangParser.GlobalFuncDeclarationContext context)
        {
            var funcCtx = context.funcDecl();
            string name = funcCtx.IDENTIFIER().GetText();

            currentFunction = Functions.FirstOrDefault(f => f.Name == name);
            if (currentFunction == null) return null;

            scopes.Push(new Dictionary<string, Symbol>());

            foreach (var param in currentFunction.Parameters)
            {
                if (scopes.Peek().ContainsKey(param.Name))
                    AddError(context.Start.Line, $"Parameter '{param.Name}' is used twice.");
                else
                    scopes.Peek()[param.Name] = param;
            }

            Visit(funcCtx.block());

            if (currentFunction.Type != "void" && !currentFunction.HasReturn)
                AddError(context.Start.Line, $"Function '{name}' needs to return a value.");

            scopes.Pop();
            currentFunction = null;
            return null;
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
                AddError(ctx.Start.Line, $"Variable '{name}' is already defined here.");
            }
            else
            {
                if (valType != null && !IsCompatible(type, valType))
                    AddError(ctx.Start.Line, $"Type mismatch for '{name}'. Expected: {type}, Got: {valType}");

                var sym = new Symbol { Name = name, Type = type, IsConst = isConst, InitValue = initVal };
                scopes.Peek()[name] = sym;

                if (isGlobal) GlobalVars.Add(sym);
                else if (currentFunction != null) currentFunction.LocalVars.Add(sym);
            }
        }

        public override string VisitBlockStmt(MiniLangParser.BlockStmtContext context)
        {
            scopes.Push(new Dictionary<string, Symbol>());
            base.VisitBlockStmt(context);
            scopes.Pop();
            return null;
        }

        public override string VisitIfStmt(MiniLangParser.IfStmtContext context)
        {
            if (currentFunction != null)
                currentFunction.ControlStructures.Add($"<if, lines {context.Start.Line}-{context.Stop.Line}>");
            Visit(context.expression());
            Visit(context.block(0));
            if (context.block().Length > 1) Visit(context.block(1));
            return null;
        }

        public override string VisitWhileStmt(MiniLangParser.WhileStmtContext context)
        {
            if (currentFunction != null)
                currentFunction.ControlStructures.Add($"<while, lines {context.Start.Line}-{context.Stop.Line}>");
            Visit(context.expression());
            Visit(context.block());
            return null;
        }

        public override string VisitForStmt(MiniLangParser.ForStmtContext context)
        {
            if (currentFunction != null)
                currentFunction.ControlStructures.Add($"<for, lines {context.Start.Line}-{context.Stop.Line}>");

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
                AddError(context.Start.Line, $"Wrong return type in '{currentFunction.Name}'. Expected {currentFunction.Type}, got {exprType}");

            return null;
        }

        public override string VisitAssignStmt(MiniLangParser.AssignStmtContext context)
        {
            string varName = context.IDENTIFIER().GetText();
            Symbol sym = Resolve(varName);

            if (sym == null)
            {
                AddError(context.Start.Line, $"Variable '{varName}' not found.");
                return "unknown";
            }
            if (sym.IsConst)
                AddError(context.Start.Line, $"Cannot change constant '{varName}'.");

            if (context.expression() != null)
            {
                string exprType = Visit(context.expression());
                if (!IsCompatible(sym.Type, exprType))
                    AddError(context.Start.Line, $"Cannot assign {exprType} to {sym.Type}.");
            }
            return sym.Type;
        }

        public override string VisitFuncCallExpr(MiniLangParser.FuncCallExprContext context)
        {
            string funcName = context.IDENTIFIER().GetText();
            var targetFunc = Functions.FirstOrDefault(f => f.Name == funcName);

            if (targetFunc == null)
            {
                AddError(context.Start.Line, $"Function '{funcName}' does not exist.");
                return "unknown";
            }

            if (targetFunc.IsMain)
                AddError(context.Start.Line, "You cannot call 'main'.");

            if (currentFunction != null && currentFunction.Name == funcName)
                currentFunction.IsRecursive = true;

            var args = context.args() != null ? context.args().expression() : new MiniLangParser.ExpressionContext[0];

            if (args.Length != targetFunc.Parameters.Count)
            {
                AddError(context.Start.Line, $"Wrong number of arguments for '{funcName}'.");
            }
            else
            {
                for (int i = 0; i < args.Length; i++)
                {
                    string argType = Visit(args[i]);
                    string paramType = targetFunc.Parameters[i].Type;
                    if (!IsCompatible(paramType, argType))
                        AddError(context.Start.Line, $"Argument {i + 1} type mismatch. Expected {paramType}, got {argType}");
                }
            }
            return targetFunc.Type;
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

        public override string VisitRelationalExpr(MiniLangParser.RelationalExprContext context) { Visit(context.expression(0)); Visit(context.expression(1)); return "int"; }
        public override string VisitEqualityExpr(MiniLangParser.EqualityExprContext context) { Visit(context.expression(0)); Visit(context.expression(1)); return "int"; }
        public override string VisitLogicAndExpr(MiniLangParser.LogicAndExprContext context) { Visit(context.expression(0)); Visit(context.expression(1)); return "int"; }
        public override string VisitLogicOrExpr(MiniLangParser.LogicOrExprContext context) { Visit(context.expression(0)); Visit(context.expression(1)); return "int"; }
        public override string VisitNotExpr(MiniLangParser.NotExprContext context) { Visit(context.expression()); return "int"; }
        public override string VisitParenExpr(MiniLangParser.ParenExprContext context) { return Visit(context.expression()); }
        public override string VisitAssignmentExpr(MiniLangParser.AssignmentExprContext context) { return Visit(context.assignStmt()); }

        public override string VisitIdExpr(MiniLangParser.IdExprContext context)
        {
            string name = context.IDENTIFIER().GetText();
            Symbol sym = Resolve(name);
            if (sym == null)
            {
                AddError(context.Start.Line, $"Variable '{name}' not found.");
                return "unknown";
            }
            return sym.Type;
        }

        public override string VisitIntExpr(MiniLangParser.IntExprContext context) => "int";
        public override string VisitFloatExpr(MiniLangParser.FloatExprContext context) => "float";
        public override string VisitStringExpr(MiniLangParser.StringExprContext context) => "string";
    }
}