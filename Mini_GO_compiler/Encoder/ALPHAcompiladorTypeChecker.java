package TypeChecker;

import SintaxChecker.generated.ALPHAcompiladorBaseVisitor;
import SintaxChecker.generated.ALPHAcompiladorLexer;
import SintaxChecker.generated.ALPHAcompiladorParser;
import org.antlr.v4.runtime.Token;

import java.util.LinkedHashMap;
import java.util.LinkedList;
import java.util.Objects;

public class ALPHAcompiladorTypeChecker extends ALPHAcompiladorBaseVisitor<Object> {
    private TablaSimbolos tableSymbol;
    private LinkedList<String> errorList;

    public ALPHAcompiladorTypeChecker() {
        this.tableSymbol = new  TablaSimbolos();
        this.errorList = new LinkedList<>();
    }

    @Override
    public Object visitProgram(ALPHAcompiladorParser.ProgramContext ctx) {
        return super.visitProgram(ctx);
    }

    @Override
    public Object visitWhileSingleCommand(ALPHAcompiladorParser.WhileSingleCommandContext ctx) {
        int exprType = -1;
        try{
            exprType = (int) visit(ctx.expression());
            if(exprType != 2){
                reportError("El tipo requerido era boolean, sin embargo se recibió "+verifyType(exprType),ctx.WHILE().getSymbol());
            }

        }
        catch(TypeErrorException e){}

        return super.visitWhileSingleCommand(ctx);
    }

    private int verifyType(String text){
        if (text.equals("int")) {
            return 0;
        }
        else if (text.equals("char")) {
            return 1;
        }
        else if (text.equals("boolean")) {
            return 2;
        }
        else if (text.equals("string")) {
            return 3;
        }
        else
            return -1;
    }

    private String verifyType(int typeNum){
        return switch (typeNum) {
            case 0 -> "int";
            case 1 -> "char";
            case 2 -> "boolean";
            case 3 -> "string";
            case 4 -> "void";
            default -> "??";
        };
    }

    private int verifyOperatorTypes(Token op, int t1, int t2){
        int returnType =-1;
        switch (op.getType()) {
            case (ALPHAcompiladorLexer.ADD):{
                if((t1==0) && t2==0){
                    returnType = 0;
                }
                else if((t1==3) && t2==3){
                    returnType = 3;
                }
                break;
            }
            case (ALPHAcompiladorLexer.SUB):{
                if((t1==0) && t2==0){
                    returnType = 0;
                }
                break;
            }
            case (ALPHAcompiladorLexer.MUL):{
                if((t1==0) && t2==0){
                    returnType = 0;
                }
                break;
            }
            case (ALPHAcompiladorLexer.DIV):{
                if((t1==0) && t2==0){
                    returnType = 0;
                }
                break;
            }
            case (ALPHAcompiladorLexer.MOD):{
                if((t1==0) && t2==0){
                    returnType = 0;
                }
                break;
            }
            case (ALPHAcompiladorLexer.EQEQ):{
                if(((t1==0) && (t2==0)) || ((t1==1) && (t2==1)) || ((t1==2) && (t2==2)) || ((t1==3) && (t2==3))){
                    returnType = 2;
                }
                break;
            }
            case (ALPHAcompiladorLexer.NOTEQ):{
                if(((t1==0) && (t2==0)) || ((t1==1) && (t2==1)) || ((t1==2) && (t2==2)) || ((t1==3) && (t2==3))){
                    returnType = 2;
                }
                break;
            }
            case (ALPHAcompiladorLexer.LESS):{
                if(((t1==0) && (t2==0)) || ((t1==1) && (t2==1))  || ((t1==3) && (t2==3))){
                    returnType = 2;
                }
                break;
            }
            case (ALPHAcompiladorLexer.MORET):{
                if(((t1==0) && (t2==0)) || ((t1==1) && (t2==1))  || ((t1==3) && (t2==3))){
                    returnType = 2;
                }
                break;
            }
            case (ALPHAcompiladorLexer.LESSEQ):{
                if(((t1==0) && (t2==0)) || ((t1==1) && (t2==1))  || ((t1==3) && (t2==3))){
                    returnType = 2;
                }
                break;
            }
            case (ALPHAcompiladorLexer.MOREEQ):{
                if(((t1==0) && (t2==0)) || ((t1==1) && (t2==1))  || ((t1==3) && (t2==3))){
                    returnType = 2;
                }
                break;
            }
        }
        return returnType;
    }

    public boolean hasErrors(){
        return !this.errorList.isEmpty();
    }

    private void reportError(String error, Token offendingToken){
        this.errorList.add("TYPE ERROR: " + error + " en (Line: " + offendingToken.getLine() + ", column: "+ offendingToken.getCharPositionInLine() + ")");
    }

    public LinkedList<String> getErrorList() {
        return errorList;
    }

    @Override
    public Object visitVarSingleDeclaration(ALPHAcompiladorParser.VarSingleDeclarationContext ctx) {
        Token id = (Token) visit(ctx.typeDenoter());
        int type = verifyType(id.getText());
        if(type != -1){
            TablaSimbolos.Ident Ident= this.tableSymbol.buscarNivelActual(ctx.IDENTIFIER().getText());
            if(Ident==null){
                this.tableSymbol.insertarVariable(ctx.IDENTIFIER().getSymbol(),type,ctx);
                this.tableSymbol.imprimir();
            }
            else{
                if(Ident instanceof TablaSimbolos.MethodIdent){
                    this.tableSymbol.insertarVariable(ctx.IDENTIFIER().getSymbol(),type,ctx);
                    this.tableSymbol.imprimir();
                }
                else{
                    reportError("La variable '"+ctx.IDENTIFIER() +"' ya ha sido definido en el scope",id);
                }
            }
        }
        else{
            reportError("No existe el tipo de variable "+id.getText()+" en el scope actual",id);
        }
        return null;
    }

    @Override
    public Object visitTypeDenoter(ALPHAcompiladorParser.TypeDenoterContext ctx) {
        if(ctx.IDENTIFIER()!=null){
            return ctx.IDENTIFIER().getSymbol();
        }
        else if(ctx.INTWORD()!=null){
            return ctx.INTWORD().getSymbol();
        }
        else if(ctx.BOOLEANWORD()!=null){
            return ctx.BOOLEANWORD().getSymbol();
        }
        else if(ctx.STRINGWORD()!=null){
            return ctx.STRINGWORD().getSymbol();
        }
        else if(ctx.CHARWORD()!=null){
            return ctx.CHARWORD().getSymbol();
        }
        //IMPOSIBLE
        return null;
    }

    @Override
    public Object visitTruePrimaryExpression(ALPHAcompiladorParser.TruePrimaryExpressionContext ctx) {
        return 2;
    }

    @Override
    public Object visitStringPrimaryExpression(ALPHAcompiladorParser.StringPrimaryExpressionContext ctx) {
        return 3;
    }

    @Override
    public Object visitOperator(ALPHAcompiladorParser.OperatorContext ctx) {
        if(ctx.ADD()!=null){
            return ctx.ADD().getSymbol();
        }
        else if(ctx.SUB()!=null){
            return ctx.SUB().getSymbol();
        }
        else if(ctx.MUL()!=null){
            return ctx.MUL().getSymbol();
        }
        else if(ctx.DIV()!=null){
            return ctx.DIV().getSymbol();
        }
        else if(ctx.MOD()!=null){
            return ctx.MOD().getSymbol();
        }
        else if(ctx.EQEQ()!=null){
            return ctx.EQEQ().getSymbol();
        }
        else if(ctx.NOTEQ()!=null){
            return ctx.NOTEQ().getSymbol();
        }
        else if(ctx.LESS()!=null){
            return ctx.LESS().getSymbol();
        }
        else if(ctx.MORET()!=null){
            return ctx.MORET().getSymbol();
        }
        else if(ctx.LESSEQ()!=null){
            return ctx.LESSEQ().getSymbol();
        }
        else if(ctx.MOREEQ()!=null){
            return ctx.MOREEQ().getSymbol();
        }
        else{
            //IMPOSIBLE
            return null;
        }
    }

    @Override
    public Object visitNumPrimaryExpression(ALPHAcompiladorParser.NumPrimaryExpressionContext ctx) {
        return 0;
    }

    @Override
    public Object visitMethodCallSingleCommand(ALPHAcompiladorParser.MethodCallSingleCommandContext ctx) {
        TablaSimbolos.MethodIdent ident = (TablaSimbolos.MethodIdent) this.tableSymbol.buscar(ctx.IDENTIFIER().getText());
        if(ident != null){
            try{
                if(ctx.argumentList()!=null){
                    LinkedList<Integer> params = (LinkedList<Integer>) visit(ctx.argumentList());
                    if(ident.params.size() != params.size()){
                        reportError("Cantidad de argumentos inválido al llamar al método '"+ctx.IDENTIFIER().getText()+"', se esperaban "+ident.params.size()+" y se recibieron "+params.size()+" argumentos",ctx.IDENTIFIER().getSymbol());
                    }
                    else{
                        for(int i = 0; i < params.size(); i++){
                            if(!ident.params.get(i).equals(params.get(i))){
                                reportError("El Tipo solicitado del argumento número "+(i+1)+" era "+ verifyType(ident.params.get(i)) +", Sin embargo, se recibió "+verifyType(params.get(i)),ctx.IDENTIFIER().getSymbol());
                            }
                        }
                    }
                }
                else{
                    if(!ident.params.isEmpty()){
                        reportError("Cantidad de argumentos inválido al llamar al método '"+ctx.IDENTIFIER().getText()+"', se esperaban "+ident.params.size()+" y se recibieron 0 argumentos",ctx.IDENTIFIER().getSymbol());
                    }
                }
            }
            catch(TypeErrorException e){}
        }
        else if(Objects.equals(ctx.IDENTIFIER().getText(), "print")){
            try{
                if(ctx.argumentList()!=null){
                    LinkedList<Integer> params = (LinkedList<Integer>) visit(ctx.argumentList());
                    if(params.size()>1){
                        reportError("Cantidad de argumentos inválido al llamar al método "+ctx.IDENTIFIER().getText()+ ", se esperaba 1 argumento y se recibieron "+params.size()+" argumentos",ctx.IDENTIFIER().getSymbol());
                    }
                }
                else{
                    reportError("Cantidad de argumentos inválido al llamar al método "+ctx.IDENTIFIER().getText()+ ", se esperaba 1 argumento y se recibieron 0 argumentos",ctx.IDENTIFIER().getSymbol());
                }
            }
            catch(TypeErrorException e){}
        }
        else{
            reportError("El método '"+ctx.IDENTIFIER().getText() +"' no existe",ctx.IDENTIFIER().getSymbol());
        }

        return null;
    }

    @Override
    public Object visitLetSingleCommand(ALPHAcompiladorParser.LetSingleCommandContext ctx) {
        this.tableSymbol.openScope();
        visit(ctx.declaration());
        visit(ctx.singleCommand());
        this.tableSymbol.closeScope();
        return null;
    }

    @Override
    public Object visitIfSingleCommand(ALPHAcompiladorParser.IfSingleCommandContext ctx) {
        int exprType = -1;
        try{
            exprType = (int) visit(ctx.expression());
            if(exprType != 2){
                reportError("El tipo requerido era boolean, sin embargo se recibió "+verifyType(exprType),ctx.IF().getSymbol());
            }

        }
        catch(TypeErrorException e){}

        return super.visitIfSingleCommand(ctx);
    }

    @Override
    public Object visitIdPrimaryExpression(ALPHAcompiladorParser.IdPrimaryExpressionContext ctx) {
        int returnType = -1;
        TablaSimbolos.Ident ident = this.tableSymbol.buscar(ctx.identifier().IDENTIFIER().getText());
        if(ident != null){
            if(ident instanceof TablaSimbolos.VarIdent){
                returnType = ident.type;
                ctx.identifier().decl = ident.decl;
            }else{
                reportError("El identificador no es una variable en el scope actual",ctx.identifier().IDENTIFIER().getSymbol());
            }
        }
        else{
            reportError("Identificador desconocido",ctx.identifier().IDENTIFIER().getSymbol());
        }
        return returnType; //TODO: O se retorna -1 o se lanza una excepcion, teniendo en cuenta que hay que cambiar el codigo hacia arriba en cualquiera de los casos
    }

    @Override
    public Object visitGroupPrimaryExpression(ALPHAcompiladorParser.GroupPrimaryExpressionContext ctx) {
        int exprType = -1;
        try{
            exprType = (int) visit(ctx.expression());
            if(ctx.SUB()!=null && exprType != 0){
                reportError("El operador unario '-' esperaba int, sin embargo recibió "+verifyType(exprType),ctx.SUB().getSymbol());
            }

        }
        catch(TypeErrorException e){}
        return exprType;
    }

    @Override
    public Object visitFalsePrimaryExpression(ALPHAcompiladorParser.FalsePrimaryExpressionContext ctx) {
        return 2;
    }

    @Override
    public Object visitMethodCallPrimaryExpression(ALPHAcompiladorParser.MethodCallPrimaryExpressionContext ctx) {
        TablaSimbolos.MethodIdent ident = (TablaSimbolos.MethodIdent) this.tableSymbol.buscar(ctx.IDENTIFIER().getText());
        if(ident != null){
            try{
                if(ctx.SUB() != null && this.tableSymbol.buscar(ctx.IDENTIFIER().getText()).type!=0){
                    reportError("El operador unario '-' esperaba int, sin embargo recibió "+verifyType(this.tableSymbol.buscar(ctx.IDENTIFIER().getText()).type),ctx.IDENTIFIER().getSymbol());
                    throw new TypeErrorException();
                }
                if(ctx.argumentList()!=null){
                    LinkedList<Integer> params = (LinkedList<Integer>) visit(ctx.argumentList());
                    if(ident.params.size() != params.size()){
                        reportError("Cantidad de argumentos inválido al llamar al método '"+ctx.IDENTIFIER().getText()+"', se esperaban "+ident.params.size()+" y se recibieron "+params.size()+" argumentos",ctx.IDENTIFIER().getSymbol());
                        throw new TypeErrorException();
                    }
                    else{
                        for(int i = 0; i < params.size(); i++){
                            if(!ident.params.get(i).equals(params.get(i))){
                                reportError("El Tipo solicitado del argumento número "+(i+1)+" era "+ verifyType(ident.params.get(i)) +", Sin embargo, se recibió "+verifyType(params.get(i)),ctx.IDENTIFIER().getSymbol());
                                throw new TypeErrorException();
                            }
                        }
                        return ident.type;
                    }
                }
                else{
                    if(!ident.params.isEmpty()){
                        reportError("Cantidad de argumentos inválido al llamar al método '"+ctx.IDENTIFIER().getText()+"', se esperaban "+ident.params.size()+" y se recibieron 0 argumentos",ctx.IDENTIFIER().getSymbol());
                        throw new TypeErrorException();
                    }
                }
            }
            catch(TypeErrorException e){}
        }
        else if(Objects.equals(ctx.IDENTIFIER().getText(), "print")){
            try{
                if(ctx.argumentList()!=null){
                    LinkedList<Integer> params = (LinkedList<Integer>) visit(ctx.argumentList());
                    if(params.size()>1){
                        reportError("Cantidad de argumentos inválido al llamar al método "+ctx.IDENTIFIER().getText()+ "se esperaba 1 argumento y se recibieron "+params.size()+" argumentos",ctx.IDENTIFIER().getSymbol());
                    }
                }
                else{
                    reportError("Cantidad de argumentos inválido al llamar al método "+ctx.IDENTIFIER().getText()+ "se esperaba 1 argumento y se recibieron 0 argumentos",ctx.IDENTIFIER().getSymbol());
                }
            }
            catch(TypeErrorException e){}
        }
        else{
            reportError("El método '"+ctx.IDENTIFIER().getText() +"' no existe en el scope actual",ctx.IDENTIFIER().getSymbol());
        }
        return ident.type;
    }

    @Override
    public Object visitExpression(ALPHAcompiladorParser.ExpressionContext ctx) {
        int returnType = -1;
        returnType = (int) visit(ctx.primaryExpression(0));
        //visit(ctx.primaryExpression(0));
        for(int i=1;i<ctx.primaryExpression().size();i++){
            Token op = (Token) visit(ctx.operator(i-1));
            int expr2 = (int) visit(ctx.primaryExpression(i));
            returnType = verifyOperatorTypes(op, returnType, expr2);
            if(returnType ==-1){
                reportError("Tipo de operación no encontrada",op);
                throw new TypeErrorException();
            }
        }
        return returnType;
    }

    @Override
    public Object visitDeclaration(ALPHAcompiladorParser.DeclarationContext ctx) {
        return super.visitDeclaration(ctx);
    }

    @Override
    public Object visitAdvanceDeclaration(ALPHAcompiladorParser.AdvanceDeclarationContext ctx) {
        Token id=null;
        int type=-1;
        if(ctx.typeDenoter()!=null){
            id = (Token) visit(ctx.typeDenoter());
            type = verifyType(id.getText());
        }else{
            type=4;
        }
        if(type!=-1){
            TablaSimbolos.Ident ident = this.tableSymbol.buscarNivelActual(ctx.IDENTIFIER().getText());
            //cree la lita de tipos para argumentos
            LinkedHashMap<Token,Integer> params = null;
            if(ctx.paramList()==null){
                params = new LinkedHashMap<>();
            }
            else {
                params = (LinkedHashMap<Token, Integer>) visit(ctx.paramList());
                for(Token token : params.keySet()){
                    if(params.get(token)==-1){
                        for(int i=0;i<errorList.size();i++){
                            System.out.println(errorList.get(i));
                        }
                        throw new TypeErrorException();
                    }
                }
            }
            LinkedList<Integer> onlyValues = new LinkedList<>();
            for(Token s : params.keySet()){
                onlyValues.add(params.get(s));
            }
            if (ident==null) {
                this.tableSymbol.insertarMetodo(ctx.IDENTIFIER().getSymbol(), type, onlyValues, ctx);
                this.tableSymbol.imprimir();
            }
            else{
                if (ident instanceof TablaSimbolos.VarIdent){
                    this.tableSymbol.insertarMetodo(ctx.IDENTIFIER().getSymbol(), type, onlyValues, ctx);
                    this.tableSymbol.imprimir();
                }else
                    reportError("Método ya definido",ctx.IDENTIFIER().getSymbol());
            }
            this.tableSymbol.openScope();
            for(Token key : params.keySet()){
                this.tableSymbol.insertarVariable(key,params.get(key),ctx);
            }

            this.tableSymbol.setMetodoActual(this.tableSymbol.buscar(ctx.IDENTIFIER().getText()));
            this.tableSymbol.imprimir();
            visit(ctx.command());
            this.tableSymbol.restartMetodoActual();
            this.tableSymbol.closeScope();

        }else{
            reportError("Tipo de operación no encontrada para la función",ctx.IDENTIFIER().getSymbol());
        }
        return null;
    }

    @Override
    public Object visitParamList(ALPHAcompiladorParser.ParamListContext ctx) {
        LinkedHashMap<Token,Integer> resultList = new LinkedHashMap<>();
        for (ALPHAcompiladorParser.ParamContext p : ctx.param()){
            LinkedHashMap<Token,Integer> param = (LinkedHashMap<Token,Integer>) visit(p);
            for(Token key : param.keySet()){
                resultList.put(key, param.get(key));
            }
        }
        return resultList;
    }

    @Override
    public Object visitParam(ALPHAcompiladorParser.ParamContext ctx) {
        int type=-1;
        Token t = (Token) visit(ctx.typeDenoter());
        type = verifyType(t.getText());
        if (type==-1)
            reportError("Tipo inválido para el argumento '"+ctx.IDENTIFIER().getText()+"'",t);
        LinkedHashMap<Token,Integer> map = new LinkedHashMap<>();
        map.put(ctx.IDENTIFIER().getSymbol(), type);
        return map;
    }

    @Override
    public Object visitArgumentList(ALPHAcompiladorParser.ArgumentListContext ctx) {
        LinkedList<Integer> resultList = new LinkedList<>();
        try{
            for(int i=0; i<ctx.expression().size();i++){
                resultList.add((int) visit(ctx.expression(i)));
            }
        }catch(TypeErrorException e){}
        return resultList;
    }

    @Override
    public Object visitConstSingleDeclaration(ALPHAcompiladorParser.ConstSingleDeclarationContext ctx) {
        try{
            int token = (int) visit(ctx.expression());
            if(token!=-1){
                TablaSimbolos.Ident ident = this.tableSymbol.buscarNivelActual(ctx.IDENTIFIER().getText());
                if(ident == null){
                    this.tableSymbol.insertarVariable(ctx.IDENTIFIER().getSymbol(),token,ctx,true);
                    tableSymbol.imprimir();
                }
                else{
                    if(ident instanceof TablaSimbolos.MethodIdent){
                        this.tableSymbol.insertarVariable(ctx.IDENTIFIER().getSymbol(), token, ctx,true);
                        tableSymbol.imprimir();
                    }
                    else{
                        reportError("La contante "+ctx.IDENTIFIER().getText() +" ya fue definida",ctx.IDENTIFIER().getSymbol());
                    }
                }

            }else{
                reportError("No existe el tipo de variable "+verifyType(token)+" en el scope actual",ctx.CONST().getSymbol());
            }
        }catch(TypeErrorException e){}

        return null;
    }

    @Override
    public Object visitCommand(ALPHAcompiladorParser.CommandContext ctx) {
        return super.visitCommand(ctx);
    }

    @Override
    public Object visitCharPrimaryExpression(ALPHAcompiladorParser.CharPrimaryExpressionContext ctx) {
        return 1;
    }

    @Override
    public Object visitBlockSingleCommand(ALPHAcompiladorParser.BlockSingleCommandContext ctx) {
        return super.visitBlockSingleCommand(ctx);
    }

    @Override
    public Object visitReturnSingleCommand(ALPHAcompiladorParser.ReturnSingleCommandContext ctx) {
        try{
            int exprType = 4;
            if(ctx.expression()!=null){
                exprType = (int) visit(ctx.expression());
            }
            if(this.tableSymbol.getMetodoActual()!=null){
                if(exprType!=this.tableSymbol.getMetodoActual().type){
                    reportError("El método "+this.tableSymbol.getMetodoActual().tok.getText()+" devuelve " +verifyType(tableSymbol.getMetodoActual().type)+", sin embargo, el return devuelve "+verifyType(exprType),ctx.RETURN().getSymbol());
                }
            }
            else{
                reportError("Llamada a return sin estar en un metodo",ctx.RETURN().getSymbol());
            }

        }
        catch(TypeErrorException e){}

        return super.visitReturnSingleCommand(ctx);
    }

    @Override
    public Object visitAssignSingleCommand(ALPHAcompiladorParser.AssignSingleCommandContext ctx) {
        try{ //TODO: Cada vez que en una regla haya un visit a expression, hay que hacer try catch
            int exprType = (int) visit(ctx.expression());
            ALPHAcompiladorParser.IdentifierContext IDENTIFIER = (ALPHAcompiladorParser.IdentifierContext) visit(ctx.identifier());
            Token ID = IDENTIFIER.IDENTIFIER().getSymbol();
            TablaSimbolos.Ident ident = this.tableSymbol.buscar(ID.getText());
            if(ident != null){
                if(ident instanceof TablaSimbolos.VarIdent){
                    if(!((TablaSimbolos.VarIdent) ident).isConstant){
                        if(ident.type != exprType){
                            reportError("Tipo inválido en una asignación, se esperaba "+verifyType(ident.type)+" pero se recibió "+verifyType(exprType),ID);
                        }
                        IDENTIFIER.decl = ident.decl; //Este es el decorado del arbol para que cada identificador tenga el contexto de su tipo y nombre
                    }
                    else{
                        reportError("No se le puede asignar a una constante",ID);
                    }
                }else{
                    reportError("No se le puede asignar a un identificador que sea metodo",ID);
                }
            }
            else{
                reportError("Identificador desconocido",ID);
            }
        }catch (TypeErrorException e){}

        return null;
    }

    @Override
    public Object visitIdentifier(ALPHAcompiladorParser.IdentifierContext ctx) {
        return ctx;
    }
}

