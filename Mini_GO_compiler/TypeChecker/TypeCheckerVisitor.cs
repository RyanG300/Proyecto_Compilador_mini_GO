using System;
using System.Collections.Generic;
using Antlr4.Runtime;
using SyntaxChecker.generated;

namespace Mini_GO_compiler.TypeChecker
{
    public class TypeCheckerVisitor : MiniGoCompilerBaseVisitor<object>
    {
        private TablaSimbolos symbolTable;
        public LinkedList<string> ErrorList { get; private set; }
        private MiniGoType currentFunctionReturnType = MiniGoType.Error;
        private bool inFunction = false;

        public TypeCheckerVisitor()
        {
            this.symbolTable = new TablaSimbolos();
            this.ErrorList = new LinkedList<string>();
        }

        public bool HasErrors() => ErrorList.Count > 0;

        private void ReportError(string msg, IToken offendingToken)
        {
            if (offendingToken != null)
                ErrorList.AddLast($"TYPE ERROR: {msg} ({offendingToken.Text}) [line {offendingToken.Line}:{offendingToken.Column}]");
            else
                ErrorList.AddLast($"TYPE ERROR: {msg}");
        }

        private MiniGoType VerifyType(string typeText)
        {
            switch (typeText)
            {
                case "int": return MiniGoType.Int;
                case "float64": 
                case "float32": return MiniGoType.Float;
                case "bool": return MiniGoType.Bool;
                case "string": return MiniGoType.String;
                case "rune": return MiniGoType.Rune;
                default: return MiniGoType.Error;
            }
        }

        public override object VisitBlock(MiniGoCompilerParser.BlockContext context)
        {
            symbolTable.OpenScope();
            base.VisitBlock(context);
            symbolTable.CloseScope();
            return null;
        }

        public override object VisitVariableDecl(MiniGoCompilerParser.VariableDeclContext context)
        {
            return base.VisitVariableDecl(context);
        }

        public override object VisitSingleVarDecl(MiniGoCompilerParser.SingleVarDeclContext context)
        {
            // identifierList declType ASSIGN expressionList
            // identifierList ASSIGN expressionList
            // singleVarDeclNoExps

            var identifierListCtx = context.identifierList();
            var declTypeCtx = context.declType();
            var expressionListCtx = context.expressionList();

            if (identifierListCtx == null)
            {
                return base.VisitSingleVarDecl(context);
            }

            var ids = identifierListCtx.IDENTIFIER();

            // Caso 1: Con tipo explícito
            if (declTypeCtx != null)
            {
                TypeInfo declaredTypeInfo = GetDeclTypeInfo(declTypeCtx);
                MiniGoType declaredType = declaredTypeInfo.BaseType;

                if (expressionListCtx != null)
                {
                    List<MiniGoType> exprTypes;
                    try
                    {
                        exprTypes = (List<MiniGoType>)Visit(expressionListCtx);
                    }
                    catch (TypeErrorException)
                    {
                        exprTypes = new List<MiniGoType>();
                        for (int i = 0; i < ids.Length; i++)
                            exprTypes.Add(MiniGoType.Error);
                    }

                    for (int i = 0; i < ids.Length; i++)
                    {
                        var idToken = ids[i].Symbol;
                        if (symbolTable.BuscarNivelActual(idToken.Text) != null)
                        {
                            ReportError("Identifier already defined!", idToken);
                        }
                        else
                        {
                            if (i < exprTypes.Count && exprTypes[i] != declaredType && exprTypes[i] != MiniGoType.Error)
                            {
                                ReportError("Invalid types in assign!", idToken);
                            }
                            symbolTable.InsertarVariableConTypeInfo(idToken, declaredTypeInfo, context);
                        }
                    }
                }
                else
                {
                    // Sin expresión
                    foreach (var id in ids)
                    {
                        if (symbolTable.BuscarNivelActual(id.Symbol.Text) != null)
                            ReportError("Identifier already defined!", id.Symbol);
                        else
                            symbolTable.InsertarVariableConTypeInfo(id.Symbol, declaredTypeInfo, context);
                    }
                }
            }
            // Caso 2: Sin tipo, Inferencia
            else if (expressionListCtx != null)
            {
                List<MiniGoType> exprTypes;
                try
                {
                    exprTypes = (List<MiniGoType>)Visit(expressionListCtx);
                }
                catch (TypeErrorException)
                {
                    exprTypes = new List<MiniGoType>();
                    for (int i = 0; i < ids.Length; i++)
                        exprTypes.Add(MiniGoType.Error);
                }

                for (int i = 0; i < ids.Length; i++)
                {
                    var idToken = ids[i].Symbol;
                    if (symbolTable.BuscarNivelActual(idToken.Text) != null)
                    {
                        ReportError("Identifier already defined!", idToken);
                    }
                    else
                    {
                        MiniGoType inferredType = (i < exprTypes.Count) ? exprTypes[i] : MiniGoType.Error;
                        symbolTable.InsertarVariable(idToken, inferredType, context);
                    }
                }
            }

            return null;
        }

        public override object VisitSingleVarDeclNoExps(MiniGoCompilerParser.SingleVarDeclNoExpsContext context)
        {
            var ids = context.identifierList().IDENTIFIER();
            TypeInfo declaredTypeInfo = GetDeclTypeInfo(context.declType());
            foreach (var id in ids)
            {
                if (symbolTable.BuscarNivelActual(id.Symbol.Text) != null)
                    ReportError("Identifier already defined!", id.Symbol);
                else
                    symbolTable.InsertarVariableConTypeInfo(id.Symbol, declaredTypeInfo, context);
            }
            return null;
        }

        public override object VisitSingleTypeDecl(MiniGoCompilerParser.SingleTypeDeclContext context)
        {
            var typeName = context.IDENTIFIER().GetText();
            var typeInfo = GetDeclTypeInfo(context.declType());

            // Si es una estructura, asignarle el nombre
            if (typeInfo.IsStruct())
            {
                typeInfo.StructName = typeName;
            }

            symbolTable.InsertarTipoUsuario(typeName, typeInfo);
            return null;
        }

        private TypeInfo GetDeclTypeInfo(MiniGoCompilerParser.DeclTypeContext context)
        {
            if (context.IDENTIFIER() != null)
            {
                string typeName = context.IDENTIFIER().GetText();

                // Verificar si es un tipo primitivo
                MiniGoType primitiveType = VerifyType(typeName);
                if (primitiveType != MiniGoType.Error)
                {
                    return TypeInfo.CreateSimple(primitiveType);
                }

                // Verificar si es un tipo definido por el usuario (struct)
                var userType = symbolTable.BuscarTipoUsuario(typeName);
                if (userType != null)
                {
                    return userType;
                }

                return TypeInfo.CreateSimple(MiniGoType.Error);
            }
            else if (context.arrayDeclType() != null)
            {
                var arrayCtx = context.arrayDeclType();
                int size = int.Parse(arrayCtx.INTLITERAL().GetText());
                var elemTypeInfo = GetDeclTypeInfo(arrayCtx.declType());
                return TypeInfo.CreateArray(elemTypeInfo.BaseType, size);
            }
            else if (context.sliceDeclType() != null)
            {
                var sliceCtx = context.sliceDeclType();
                var elemTypeInfo = GetDeclTypeInfo(sliceCtx.declType());
                return TypeInfo.CreateSlice(elemTypeInfo.BaseType);
            }
            else if (context.structDeclType() != null)
            {
                // Procesar definición de struct inline
                var structCtx = context.structDeclType();
                var members = new Dictionary<string, TypeInfo>();

                if (structCtx.structMemDecls() != null)
                {
                    foreach (var memberDecl in structCtx.structMemDecls().singleVarDeclNoExps())
                    {
                        var memberTypeInfo = GetDeclTypeInfo(memberDecl.declType());
                        foreach (var memberId in memberDecl.identifierList().IDENTIFIER())
                        {
                            members[memberId.GetText()] = memberTypeInfo;
                        }
                    }
                }

                return TypeInfo.CreateStruct(null, members);
            }
            else if (context.declType() != null)
            {
                return GetDeclTypeInfo(context.declType());
            }
            return TypeInfo.CreateSimple(MiniGoType.Error);
        }

        private MiniGoType GetDeclType(MiniGoCompilerParser.DeclTypeContext context)
        {
            return GetDeclTypeInfo(context).BaseType;
        }

        public override object VisitFuncFrontDecl(MiniGoCompilerParser.FuncFrontDeclContext context)
        {
            var idToken = context.IDENTIFIER().Symbol;
            MiniGoType returnType = MiniGoType.Void;

            if (context.declType() != null)
            {
                returnType = GetDeclType(context.declType());
            }

            LinkedList<TypeInfo> paramTypes = new LinkedList<TypeInfo>();

            if (symbolTable.BuscarNivelActual(idToken.Text) != null)
            {
                ReportError("Method already defined!", idToken);
            }
            else
            {
                symbolTable.InsertarMetodo(idToken, returnType, paramTypes, context);
            }

            currentFunctionReturnType = returnType;
            inFunction = true;

            symbolTable.OpenScope();

            // Declarar parametros en el nuevo scope
            if (context.funcArgDecls() != null)
            {
                foreach (var singleDecl in context.funcArgDecls().singleVarDeclNoExps())
                {
                    TypeInfo paramTypeInfo = GetDeclTypeInfo(singleDecl.declType());
                    paramTypes.AddLast(paramTypeInfo);
                    foreach (var paramId in singleDecl.identifierList().IDENTIFIER())
                    {
                        if (symbolTable.BuscarNivelActual(paramId.Symbol.Text) != null)
                            ReportError("Parameter already defined!", paramId.Symbol);
                        else
                            symbolTable.InsertarVariableConTypeInfo(paramId.Symbol, paramTypeInfo, singleDecl);
                    }
                }
            }

            return null;
        }

        public override object VisitFuncDecl(MiniGoCompilerParser.FuncDeclContext context)
        {
            Visit(context.funcFrontDecl());
            
            // No creamos nuevo scope aquí porque ya lo hicimos en FuncFrontDecl para incluir parametros
            base.VisitBlock(context.block());
            
            symbolTable.CloseScope();
            inFunction = false;
            currentFunctionReturnType = MiniGoType.Error;
            
            return null;
        }

        public override object VisitExpressionList(MiniGoCompilerParser.ExpressionListContext context)
        {
            List<MiniGoType> types = new List<MiniGoType>();
            foreach (var expr in context.expression())
            {
                try
                {
                    var type = (MiniGoType)Visit(expr);
                    types.Add(type);
                }
                catch (TypeErrorException)
                {
                    types.Add(MiniGoType.Error);
                }
            }
            return types;
        }

        public override object VisitExpression(MiniGoCompilerParser.ExpressionContext context)
        {
            // Este método maneja las operaciones binarias y unarias de expression
            if (context.primaryExpression() != null)
            {
                try
                {
                    return Visit(context.primaryExpression());
                }
                catch (TypeErrorException)
                {
                    return MiniGoType.Error;
                }
            }

            if (context.expression().Length == 2)
            {
                MiniGoType t1, t2;
                try
                {
                    t1 = (MiniGoType)Visit(context.expression(0));
                }
                catch (TypeErrorException)
                {
                    t1 = MiniGoType.Error;
                }

                try
                {
                    t2 = (MiniGoType)Visit(context.expression(1));
                }
                catch (TypeErrorException)
                {
                    t2 = MiniGoType.Error;
                }
                
                // Get the operator string
                string op = context.GetChild(1).GetText();
                
                MiniGoType returnType = MiniGoType.Error;
                
                // Reglas simples
                if (op == "+" || op == "-" || op == "*" || op == "/" || op == "%")
                {
                    if (t1 == MiniGoType.Int && t2 == MiniGoType.Int) returnType = MiniGoType.Int;
                    else if (t1 == MiniGoType.Float && t2 == MiniGoType.Float) returnType = MiniGoType.Float;
                    else if (op == "+" && t1 == MiniGoType.String && t2 == MiniGoType.String) returnType = MiniGoType.String;
                }
                else if (op == "==" || op == "!=" || op == "<" || op == "<=" || op == ">" || op == ">=")
                {
                    if (t1 == t2) returnType = MiniGoType.Bool;
                }
                else if (op == "&&" || op == "||")
                {
                    if (t1 == MiniGoType.Bool && t2 == MiniGoType.Bool) returnType = MiniGoType.Bool;
                }
                else if (op == "<<" || op == ">>" || op == "&" || op == "|" || op == "^" || op == "&^")
                {
                    if (t1 == MiniGoType.Int && t2 == MiniGoType.Int) returnType = MiniGoType.Int;
                }

                if (returnType == MiniGoType.Error && t1 != MiniGoType.Error && t2 != MiniGoType.Error)
                {
                    ReportError("Incompatible types for operator " + op, ((Antlr4.Runtime.Tree.TerminalNodeImpl)context.GetChild(1)).Symbol);
                }

                return returnType;
            }
            else if (context.expression().Length == 1)
            {
                // Unary operators
                string op = context.GetChild(0).GetText();
                MiniGoType t1;
                try
                {
                    t1 = (MiniGoType)Visit(context.expression(0));
                }
                catch (TypeErrorException)
                {
                    t1 = MiniGoType.Error;
                }
                
                if (op == "!")
                {
                    if (t1 == MiniGoType.Bool) return MiniGoType.Bool;
                }
                else if (op == "-" || op == "+")
                {
                    if (t1 == MiniGoType.Int || t1 == MiniGoType.Float) return t1;
                }

                if (t1 != MiniGoType.Error)
                {
                    ReportError("Incompatible type for unary operator " + op, ((Antlr4.Runtime.Tree.TerminalNodeImpl)context.GetChild(0)).Symbol);
                }
            }

            return MiniGoType.Error;
        }

        public override object VisitOperand(MiniGoCompilerParser.OperandContext context)
        {
            if (context.literal() != null)
            {
                return Visit(context.literal());
            }
            else if (context.identifier() != null && context.identifier().IDENTIFIER() != null)
            {
                var idToken = context.identifier().IDENTIFIER().Symbol;
                var idText = idToken.Text;

                // Manejar literales booleanos predefinidos
                if (idText == "true" || idText == "false")
                {
                    return MiniGoType.Bool;
                }

                var ident = symbolTable.Buscar(idText);
                if (ident == null)
                {
                    ReportError("Undefined identifier!", idToken);
                    return MiniGoType.Error;
                }
                return ident.Type;
            }
            else if (context.expression() != null)
            {
                try
                {
                    return Visit(context.expression());
                }
                catch (TypeErrorException)
                {
                    return MiniGoType.Error;
                }
            }
            return MiniGoType.Error;
        }

        public override object VisitLiteral(MiniGoCompilerParser.LiteralContext context)
        {
            if (context.INTLITERAL() != null) return MiniGoType.Int;
            if (context.FLOATLITERAL() != null) return MiniGoType.Float;
            if (context.RUNELITERAL() != null) return MiniGoType.Rune;
            if (context.RAWSTRINGLITERAL() != null) return MiniGoType.String;
            if (context.INTERPRETEDSTRINGLITERAL() != null) return MiniGoType.String;
            return MiniGoType.Error;
        }

        public override object VisitAssignmentStatement(MiniGoCompilerParser.AssignmentStatementContext context)
        {
            if (context.expressionList().Length == 2)
            {
                List<MiniGoType> t1List, t2List;
                try
                {
                    t1List = (List<MiniGoType>)Visit(context.expressionList(0));
                }
                catch (TypeErrorException)
                {
                    return null;
                }

                try
                {
                    t2List = (List<MiniGoType>)Visit(context.expressionList(1));
                }
                catch (TypeErrorException)
                {
                    return null;
                }
                
                for (int i = 0; i < Math.Min(t1List.Count, t2List.Count); i++)
                {
                    if (t1List[i] != t2List[i] && t1List[i] != MiniGoType.Error && t2List[i] != MiniGoType.Error)
                    {
                        var firstExpr = context.expressionList(0).expression(i);
                        IToken token = null;
                        if (firstExpr.primaryExpression()?.operand()?.identifier().IDENTIFIER() != null)
                            token = firstExpr.primaryExpression().operand().identifier().IDENTIFIER().Symbol;
                        
                        ReportError("Invalid types in assign!", token);
                    }
                }
            }
            return null;
        }

        public override object VisitStatement(MiniGoCompilerParser.StatementContext context)
        {
            if (context.RETURN() != null)
            {
                if (!inFunction)
                {
                    ReportError("Return outside of a function!", context.RETURN().Symbol);
                }
                else
                {
                    MiniGoType returnType = MiniGoType.Void;
                    if (context.expression() != null)
                    {
                        try
                        {
                            returnType = (MiniGoType)Visit(context.expression());
                        }
                        catch (TypeErrorException) {}
                    }

                    if (returnType != currentFunctionReturnType && returnType != MiniGoType.Error)
                    {
                        ReportError("Return type mismatch!", context.RETURN().Symbol);
                    }
                }
            }
            else if (context.PRINT() != null || context.PRINTLN() != null)
            {
                if (context.expressionList() != null)
                {
                    Visit(context.expressionList()); // Just to check the expressions are valid
                }
            }
            
            return base.VisitStatement(context);
        }

        public override object VisitPrimaryExpression(MiniGoCompilerParser.PrimaryExpressionContext context)
        {
            if (context.operand() != null)
            {
                try
                {
                    return Visit(context.operand());
                }
                catch (TypeErrorException)
                {
                    return MiniGoType.Error;
                }
            }
            else if (context.lengthExpression() != null)
            {
                return Visit(context.lengthExpression());
            }
            else if (context.capExpression() != null)
            {
                return Visit(context.capExpression());
            }
            else if (context.appendExpression() != null)
            {
                try
                {
                    return Visit(context.appendExpression());
                }
                catch (TypeErrorException)
                {
                    return MiniGoType.Error;
                }
            }
            else if (context.selector() != null)
            {
                // Validación de acceso a miembros de struct
                try
                {
                    MiniGoType baseType = (MiniGoType)Visit(context.primaryExpression());

                    // Obtener el identificador base para acceder a su TypeInfo
                    var baseIdent = GetIdentFromPrimaryExpression(context.primaryExpression());
                    if (baseIdent != null && baseIdent.TypeInfo != null && baseIdent.TypeInfo.IsStruct())
                    {
                        string memberName = context.selector().IDENTIFIER().GetText();
                        if (baseIdent.TypeInfo.StructMembers.TryGetValue(memberName, out TypeInfo memberType))
                        {
                            return memberType.BaseType;
                        }
                        else
                        {
                            ReportError($"Struct member '{memberName}' does not exist!", context.selector().IDENTIFIER().Symbol);
                            return MiniGoType.Error;
                        }
                    }
                    else if (baseType != MiniGoType.Struct && baseType != MiniGoType.Error)
                    {
                        ReportError("Selector can only be used on struct types!", context.selector().IDENTIFIER().Symbol);
                    }
                    return MiniGoType.Error;
                }
                catch (TypeErrorException)
                {
                    return MiniGoType.Error;
                }
            }
            else if (context.arguments() != null)
            {
                // INDICADOR 8: Validación de llamadas a funciones
                try
                {
                    // Obtener el identificador de la función
                    var funcIdent = GetIdentFromPrimaryExpression(context.primaryExpression());
                    if (funcIdent is TablaSimbolos.MethodIdent methodIdent)
                    {
                        // Obtener argumentos de la llamada
                        List<MiniGoType> argTypes = new List<MiniGoType>();
                        if (context.arguments().expressionList() != null)
                        {
                            argTypes = (List<MiniGoType>)Visit(context.arguments().expressionList());
                        }

                        // Validar número de argumentos
                        if (argTypes.Count != methodIdent.ParamTypes.Count)
                        {
                            ReportError($"Function '{funcIdent.Token.Text}' expects {methodIdent.ParamTypes.Count} arguments but got {argTypes.Count}!", 
                                       context.arguments().LEFTP().Symbol);
                        }
                        else
                        {
                            // Validar tipos de argumentos
                            int paramIndex = 0;
                            foreach (var paramTypeInfo in methodIdent.ParamTypes)
                            {
                                if (paramIndex < argTypes.Count)
                                {
                                    if (argTypes[paramIndex] != paramTypeInfo.BaseType && 
                                        argTypes[paramIndex] != MiniGoType.Error)
                                    {
                                        ReportError($"Function '{funcIdent.Token.Text}' parameter {paramIndex + 1} expects type '{paramTypeInfo.BaseType}' but got '{argTypes[paramIndex]}'!", 
                                                   context.arguments().LEFTP().Symbol);
                                    }
                                }
                                paramIndex++;
                            }
                        }

                        return methodIdent.Type;
                    }
                    else
                    {
                        // No es una función, error
                        if (funcIdent != null)
                        {
                            ReportError($"'{funcIdent.Token.Text}' is not a function!", context.arguments().LEFTP().Symbol);
                        }
                        return MiniGoType.Error;
                    }
                }
                catch (TypeErrorException)
                {
                    return MiniGoType.Error;
                }
            }
            else if (context.index() != null)
            {
                // Indexación de arrays: arr[index]
                try
                {
                    MiniGoType baseType = (MiniGoType)Visit(context.primaryExpression());

                    // Verificar que el índice sea un entero
                    MiniGoType indexType = (MiniGoType)Visit(context.index().expression());
                    if (indexType != MiniGoType.Int && indexType != MiniGoType.Error)
                    {
                        ReportError("Array index must be an integer!", context.index().LEFTCORCHET().Symbol);
                    }

                    // Si baseType es un array o slice, devolver el tipo del elemento
                    var baseIdent = GetIdentFromPrimaryExpression(context.primaryExpression());
                    if (baseIdent != null && baseIdent.TypeInfo != null)
                    {
                        if (baseIdent.TypeInfo.BaseType == MiniGoType.IntArray)
                        {
                            // Array de enteros: devolver Int
                            return MiniGoType.Int;
                        }
                        else if (baseIdent.TypeInfo.BaseType == MiniGoType.Slice)
                        {
                            // Slice: devolver el tipo del elemento
                            return baseIdent.TypeInfo.ElementType;
                        }
                    }

                    if (baseType != MiniGoType.Error && baseType != MiniGoType.IntArray && baseType != MiniGoType.Slice)
                    {
                        ReportError("Cannot index non-array/slice type!", context.index().LEFTCORCHET().Symbol);
                    }
                    return MiniGoType.Error;
                }
                catch (TypeErrorException)
                {
                    return MiniGoType.Error;
                }
            }

            try
            {
                return base.VisitPrimaryExpression(context);
            }
            catch (TypeErrorException)
            {
                return MiniGoType.Error;
            }
        }

        // Método auxiliar para obtener el identificador de una primaryExpression
        private TablaSimbolos.Ident GetIdentFromPrimaryExpression(MiniGoCompilerParser.PrimaryExpressionContext context)
        {
            if (context.operand() != null && context.operand().identifier().IDENTIFIER() != null)
            {
                string idName = context.operand().identifier().IDENTIFIER().GetText();
                return symbolTable.Buscar(idName);
            }
            return null;
        }
        
        public override object VisitLengthExpression(MiniGoCompilerParser.LengthExpressionContext context)
        {
            try
            {
                MiniGoType exprType = (MiniGoType)Visit(context.expression());

                // INDICADOR 1: Validar que el argumento sea array o slice
                if (exprType != MiniGoType.IntArray && exprType != MiniGoType.Slice && exprType != MiniGoType.Error)
                {
                    ReportError("len() requires an array or slice argument!", context.LEN().Symbol);
                }
            }
            catch (TypeErrorException) { }
            return MiniGoType.Int;
        }

        public override object VisitCapExpression(MiniGoCompilerParser.CapExpressionContext context)
        {
            try
            {
                MiniGoType exprType = (MiniGoType)Visit(context.expression());

                // INDICADOR 1: Validar que el argumento sea array o slice
                if (exprType != MiniGoType.IntArray && exprType != MiniGoType.Slice && exprType != MiniGoType.Error)
                {
                    ReportError("cap() requires an array or slice argument!", context.CAP().Symbol);
                }
            }
            catch (TypeErrorException) { }
            return MiniGoType.Int;
        }

        public override object VisitAppendExpression(MiniGoCompilerParser.AppendExpressionContext context)
        {
            try
            {
                // INDICADOR 1: Validar append
                MiniGoType sliceType = (MiniGoType)Visit(context.expression(0));
                MiniGoType elemType = (MiniGoType)Visit(context.expression(1));

                // Validar que el primer argumento sea un slice
                if (sliceType != MiniGoType.Slice && sliceType != MiniGoType.Error)
                {
                    ReportError("append() requires a slice as first argument!", context.APPEND().Symbol);
                    return MiniGoType.Error;
                }

                // Obtener TypeInfo del slice para verificar el tipo elemento
                var sliceIdent = GetIdentFromExpression(context.expression(0));
                if (sliceIdent != null && sliceIdent.TypeInfo != null && sliceIdent.TypeInfo.BaseType == MiniGoType.Slice)
                {
                    // Validar que el tipo del elemento coincida
                    if (elemType != sliceIdent.TypeInfo.ElementType && elemType != MiniGoType.Error)
                    {
                        ReportError($"append() element type mismatch: slice element is '{sliceIdent.TypeInfo.ElementType}' but got '{elemType}'!", 
                                   context.APPEND().Symbol);
                    }
                }

                return MiniGoType.Slice;
            }
            catch (TypeErrorException)
            {
                return MiniGoType.Error;
            }
        }

        // Método auxiliar para obtener identificador de una expresión
        private TablaSimbolos.Ident GetIdentFromExpression(MiniGoCompilerParser.ExpressionContext context)
        {
            if (context.primaryExpression() != null && 
                context.primaryExpression().operand() != null &&
                context.primaryExpression().operand().identifier().IDENTIFIER() != null)
            {
                string idName = context.primaryExpression().operand().identifier().IDENTIFIER().GetText();
                return symbolTable.Buscar(idName);
            }
            return null;
        }
    }
}
