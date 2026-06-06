package Encoder;

import SintaxChecker.generated.ALPHAcompiladorBaseVisitor;
import SintaxChecker.generated.ALPHAcompiladorParser;
import org.antlr.v4.runtime.Token;
import org.bytedeco.llvm.LLVM.*;
import org.bytedeco.javacpp.PointerPointer;

import java.io.IOException;
import java.nio.file.Files;
import java.nio.file.Path;
import java.nio.file.Paths;
import java.util.ArrayDeque;
import java.util.ArrayList;
import java.util.Deque;
import java.util.HashMap;
import java.util.List;
import java.util.Map;

import static org.bytedeco.llvm.global.LLVM.*;

public class AlphacompiladorEnconder extends ALPHAcompiladorBaseVisitor<Object> {
    private LLVMModuleRef module;
    private LLVMBuilderRef builder;

    private final LLVMTypeRef int32Type = LLVMInt32Type();
    private final LLVMTypeRef voidType = LLVMVoidType();
    private final LLVMTypeRef boolType = LLVMInt1Type();
    private final LLVMTypeRef charType = LLVMInt8Type();
    private final LLVMTypeRef stringPtrType = LLVMPointerType(LLVMInt8Type(), 0);

    private final Map<String, LLVMValueRef> methods = new HashMap<>();
    private final Map<String, LLVMTypeRef> methodTypes = new HashMap<>();
    private final Deque<Map<String, VariableInfo>> variableScopes = new ArrayDeque<>();

    private LLVMValueRef mainFunc;
    private Path outputLl;
    private Path outputExe;
    private int globalStringCounter = 0;

    private static final class VariableInfo {
        final LLVMValueRef alloca;
        final LLVMTypeRef type;

        VariableInfo(LLVMValueRef alloca, LLVMTypeRef type) {
            this.alloca = alloca;
            this.type = type;
        }
    }

    public String getGeneratedLlPath() {
        return this.outputLl == null ? null : this.outputLl.toAbsolutePath().toString();
    }

    public String getGeneratedExePath() {
        return this.outputExe == null ? null : this.outputExe.toAbsolutePath().toString();
    }

    @Override
    public Object visitProgram(ALPHAcompiladorParser.ProgramContext ctx) {
        LLVMInitializeNativeTarget();
        LLVMInitializeNativeAsmPrinter();
        LLVMInitializeNativeAsmParser();

        this.module = LLVMModuleCreateWithName("mi_modulo");
        this.builder = LLVMCreateBuilder();
        this.methods.clear();
        this.methodTypes.clear();
        this.variableScopes.clear();
        this.globalStringCounter = 0;

        super.visitProgram(ctx);

        String irText = LLVMPrintModuleToString(this.module).getString();
        System.out.println(irText);

        try {
            Path outputDir = Paths.get("target", "alpha-output").toAbsolutePath();
            Files.createDirectories(outputDir);
            this.outputLl = outputDir.resolve("program.ll");
            this.outputExe = outputDir.resolve("program.exe");
            Files.writeString(this.outputLl, irText);
            compileLlToExe(this.outputLl, this.outputExe);
        } catch (IOException e) {
            throw new RuntimeException("No se pudo escribir salida del encoder: " + e.getMessage(), e);
        }

        LLVMDisposeBuilder(this.builder);
        LLVMDisposeModule(this.module);
        return null;
    }

    @Override
    public Object visitAdvanceDeclaration(ALPHAcompiladorParser.AdvanceDeclarationContext ctx) {
        LLVMTypeRef returnType = ctx.typeDenoter() == null ? this.voidType : (LLVMTypeRef) visit(ctx.typeDenoter());
        List<ALPHAcompiladorParser.ParamContext> params = ctx.paramList() == null ? List.of() : ctx.paramList().param();

        LLVMTypeRef[] llvmParamTypes = new LLVMTypeRef[params.size()];
        for (int i = 0; i < params.size(); i++) {
            llvmParamTypes[i] = (LLVMTypeRef) visit(params.get(i).typeDenoter());
        }

        PointerPointer<LLVMTypeRef> paramPointer = llvmParamTypes.length == 0 ? null : new PointerPointer<>(llvmParamTypes);
        LLVMTypeRef functionType = LLVMFunctionType(returnType, paramPointer, llvmParamTypes.length, 0);

        String functionName = ctx.IDENTIFIER().getText();
        LLVMValueRef function = LLVMAddFunction(this.module, functionName, functionType);
        this.methods.put(functionName, function);
        this.methodTypes.put(functionName, functionType);
        if ("main".equals(functionName)) {
            this.mainFunc = function;
        }

        LLVMBasicBlockRef entry = LLVMAppendBasicBlock(function, "entry");
        LLVMPositionBuilderAtEnd(this.builder, entry);

        pushScope();
        for (int i = 0; i < params.size(); i++) {
            ALPHAcompiladorParser.ParamContext p = params.get(i);
            LLVMTypeRef paramType = llvmParamTypes[i];
            LLVMValueRef alloca = LLVMBuildAlloca(this.builder, paramType, p.IDENTIFIER().getText());
            LLVMValueRef incomingValue = LLVMGetParam(function, i);
            LLVMBuildStore(this.builder, incomingValue, alloca);
            putVariable(p.IDENTIFIER().getText(), new VariableInfo(alloca, paramType));
        }

        visit(ctx.command());

        if (!hasTerminatorInCurrentBlock()) {
            if (LLVMGetTypeKind(returnType) == LLVMVoidTypeKind) {
                LLVMBuildRetVoid(this.builder);
            } else {
                LLVMBuildRet(this.builder, defaultValueFor(returnType));
            }
        }

        popScope();
        return null;
    }

    @Override
    public Object visitArgumentList(ALPHAcompiladorParser.ArgumentListContext ctx) {
        List<LLVMValueRef> args = new ArrayList<>();
        for (ALPHAcompiladorParser.ExpressionContext expressionContext : ctx.expression()) {
            args.add((LLVMValueRef) visit(expressionContext));
        }
        return args;
    }

    @Override
    public Object visitAssignSingleCommand(ALPHAcompiladorParser.AssignSingleCommandContext ctx) {
        LLVMValueRef value = (LLVMValueRef) visit(ctx.expression());
        String variableName = ctx.identifier().IDENTIFIER().getText();
        VariableInfo variableInfo = resolveVariable(variableName);
        if (variableInfo == null) {
            throw new IllegalStateException("Variable no encontrada para asignacion: " + variableName);
        }

        LLVMBuildStore(this.builder, castValueIfNeeded(value, variableInfo.type), variableInfo.alloca);
        return null;
    }

    @Override
    public Object visitBlockSingleCommand(ALPHAcompiladorParser.BlockSingleCommandContext ctx) {
        return visit(ctx.command());
    }

    @Override
    public Object visitCharPrimaryExpression(ALPHAcompiladorParser.CharPrimaryExpressionContext ctx) {
        String raw = ctx.CHARLIT().getText();
        char decoded = decodeCharLiteral(raw);
        return LLVMConstInt(this.charType, decoded, 0);
    }

    @Override
    public Object visitCommand(ALPHAcompiladorParser.CommandContext ctx) {
        for (ALPHAcompiladorParser.SingleCommandContext singleCommandContext : ctx.singleCommand()) {
            if (hasTerminatorInCurrentBlock()) {
                break;
            }
            visit(singleCommandContext);
        }
        return null;
    }

    @Override
    public Object visitConstSingleDeclaration(ALPHAcompiladorParser.ConstSingleDeclarationContext ctx) {
        LLVMValueRef initValue = (LLVMValueRef) visit(ctx.expression());
        LLVMTypeRef valueType = LLVMTypeOf(initValue);

        LLVMValueRef storage;
        if (isInsideFunction()) {
            storage = LLVMBuildAlloca(this.builder, valueType, ctx.IDENTIFIER().getText());
            LLVMBuildStore(this.builder, initValue, storage);
        } else {
            storage = LLVMAddGlobal(this.module, valueType, ctx.IDENTIFIER().getText());
            if (LLVMIsConstant(initValue) != 0) {
                LLVMSetInitializer(storage, initValue);
            } else {
                LLVMSetInitializer(storage, defaultValueFor(valueType));
            }
        }

        putVariable(ctx.IDENTIFIER().getText(), new VariableInfo(storage, valueType));
        return null;
    }

    @Override
    public Object visitDeclaration(ALPHAcompiladorParser.DeclarationContext ctx) {
        for (ALPHAcompiladorParser.SingleDeclarationContext singleDeclarationContext : ctx.singleDeclaration()) {
            visit(singleDeclarationContext);
        }
        for (ALPHAcompiladorParser.AdvanceDeclarationContext advanceDeclarationContext : ctx.advanceDeclaration()) {
            visit(advanceDeclarationContext);
        }
        return null;
    }

    @Override
    public Object visitExpression(ALPHAcompiladorParser.ExpressionContext ctx) {
        LLVMValueRef value = (LLVMValueRef) visit(ctx.primaryExpression(0));
        for (int i = 1; i < ctx.primaryExpression().size(); i++) {
            LLVMValueRef rhs = (LLVMValueRef) visit(ctx.primaryExpression(i));
            value = applyBinaryOperator(ctx.operator(i - 1), value, rhs);
        }
        return value;
    }

    @Override
    public Object visitFalsePrimaryExpression(ALPHAcompiladorParser.FalsePrimaryExpressionContext ctx) {
        return LLVMConstInt(this.boolType, 0, 0);
    }

    @Override
    public Object visitGroupPrimaryExpression(ALPHAcompiladorParser.GroupPrimaryExpressionContext ctx) {
        LLVMValueRef value = (LLVMValueRef) visit(ctx.expression());
        if (ctx.SUB() != null) {
            return LLVMBuildNeg(this.builder, value, "negtmp");
        }
        return value;
    }

    @Override
    public Object visitIdentifier(ALPHAcompiladorParser.IdentifierContext ctx) {
        return ctx;
    }

    @Override
    public Object visitIdPrimaryExpression(ALPHAcompiladorParser.IdPrimaryExpressionContext ctx) {
        String variableName = ctx.identifier().IDENTIFIER().getText();
        VariableInfo variableInfo = resolveVariable(variableName);
        if (variableInfo == null) {
            throw new IllegalStateException("Variable no encontrada: " + variableName);
        }
        return LLVMBuildLoad2(this.builder, variableInfo.type, variableInfo.alloca, variableName + "_val");
    }

    @Override
    public Object visitIfSingleCommand(ALPHAcompiladorParser.IfSingleCommandContext ctx) {
        LLVMValueRef condition = ensureI1((LLVMValueRef) visit(ctx.expression()));
        LLVMValueRef currentFunction = LLVMGetBasicBlockParent(LLVMGetInsertBlock(this.builder));

        LLVMBasicBlockRef thenBlock = LLVMAppendBasicBlock(currentFunction, "if.then");
        LLVMBasicBlockRef elseBlock = ctx.singleCommand().size() > 1 ? LLVMAppendBasicBlock(currentFunction, "if.else") : null;
        LLVMBasicBlockRef mergeBlock = LLVMAppendBasicBlock(currentFunction, "if.end");

        if (elseBlock != null) {
            LLVMBuildCondBr(this.builder, condition, thenBlock, elseBlock);
        } else {
            LLVMBuildCondBr(this.builder, condition, thenBlock, mergeBlock);
        }

        LLVMPositionBuilderAtEnd(this.builder, thenBlock);
        visit(ctx.singleCommand(0));
        if (!hasTerminatorInCurrentBlock()) {
            LLVMBuildBr(this.builder, mergeBlock);
        }

        if (elseBlock != null) {
            LLVMPositionBuilderAtEnd(this.builder, elseBlock);
            visit(ctx.singleCommand(1));
            if (!hasTerminatorInCurrentBlock()) {
                LLVMBuildBr(this.builder, mergeBlock);
            }
        }

        LLVMPositionBuilderAtEnd(this.builder, mergeBlock);
        return null;
    }

    @Override
    public Object visitLetSingleCommand(ALPHAcompiladorParser.LetSingleCommandContext ctx) {
        pushScope();
        visit(ctx.declaration());
        visit(ctx.singleCommand());
        popScope();
        return null;
    }

    @Override
    public Object visitMethodCallPrimaryExpression(ALPHAcompiladorParser.MethodCallPrimaryExpressionContext ctx) {
        LLVMValueRef function = this.methods.get(ctx.IDENTIFIER().getText());
        if (function == null) {
            throw new IllegalStateException("Metodo no encontrado: " + ctx.IDENTIFIER().getText());
        }

        List<LLVMValueRef> args = ctx.argumentList() == null ? List.of() : (List<LLVMValueRef>) visit(ctx.argumentList());
        PointerPointer<LLVMValueRef> argValues = args.isEmpty() ? null : new PointerPointer<>(args.toArray(new LLVMValueRef[0]));

        LLVMTypeRef functionType = this.methodTypes.get(ctx.IDENTIFIER().getText());
        if (functionType == null) {
            throw new IllegalStateException("Tipo de metodo no encontrado: " + ctx.IDENTIFIER().getText());
        }

        LLVMValueRef callValue = LLVMBuildCall2(this.builder, functionType, function, argValues, args.size(), "calltmp");
        if (ctx.SUB() != null) {
            return LLVMBuildNeg(this.builder, callValue, "negcall");
        }
        return callValue;
    }

    @Override
    public Object visitMethodCallSingleCommand(ALPHAcompiladorParser.MethodCallSingleCommandContext ctx) {
        if ("print".equals(ctx.IDENTIFIER().getText())) {
            if (ctx.argumentList() == null || ctx.argumentList().expression().isEmpty()) {
                throw new IllegalStateException("print requiere un argumento");
            }
            LLVMValueRef valueExpr = (LLVMValueRef) visit(ctx.argumentList().expression(0));
            emitPrint(valueExpr);
            return null;
        }

        LLVMValueRef function = this.methods.get(ctx.IDENTIFIER().getText());
        if (function == null) {
            throw new IllegalStateException("Metodo no encontrado: " + ctx.IDENTIFIER().getText());
        }

        List<LLVMValueRef> args = ctx.argumentList() == null ? List.of() : (List<LLVMValueRef>) visit(ctx.argumentList());
        PointerPointer<LLVMValueRef> argValues = args.isEmpty() ? null : new PointerPointer<>(args.toArray(new LLVMValueRef[0]));
        LLVMTypeRef functionType = this.methodTypes.get(ctx.IDENTIFIER().getText());
        if (functionType == null) {
            throw new IllegalStateException("Tipo de metodo no encontrado: " + ctx.IDENTIFIER().getText());
        }
        LLVMBuildCall2(this.builder, functionType, function, argValues, args.size(), "");
        return null;
    }

    @Override
    public Object visitNumPrimaryExpression(ALPHAcompiladorParser.NumPrimaryExpressionContext ctx) {
        long value = Long.parseLong(ctx.LITERAL().getText());
        LLVMValueRef literal = LLVMConstInt(this.int32Type, value, 0);
        if (ctx.SUB() != null) {
            return LLVMBuildNeg(this.builder, literal, "neglit");
        }
        return literal;
    }

    @Override
    public Object visitOperator(ALPHAcompiladorParser.OperatorContext ctx) {
        return ctx;
    }

    @Override
    public Object visitParam(ALPHAcompiladorParser.ParamContext ctx) {
        return null;
    }

    @Override
    public Object visitParamList(ALPHAcompiladorParser.ParamListContext ctx) {
        return null;
    }

    @Override
    public Object visitReturnSingleCommand(ALPHAcompiladorParser.ReturnSingleCommandContext ctx) {
        if (ctx.expression() == null) {
            LLVMBuildRetVoid(this.builder);
        } else {
            LLVMBuildRet(this.builder, (LLVMValueRef) visit(ctx.expression()));
        }
        return null;
    }

    @Override
    public Object visitStringPrimaryExpression(ALPHAcompiladorParser.StringPrimaryExpressionContext ctx) {
        String literal = decodeStringLiteral(ctx.STRINGLIT().getText());
        if (isInsideFunction()) {
            return LLVMBuildGlobalStringPtr(this.builder, literal, "str");
        }
        return createModuleGlobalStringPtr(literal);
    }

    @Override
    public Object visitTruePrimaryExpression(ALPHAcompiladorParser.TruePrimaryExpressionContext ctx) {
        return LLVMConstInt(this.boolType, 1, 0);
    }

    @Override
    public Object visitTypeDenoter(ALPHAcompiladorParser.TypeDenoterContext ctx) {
        if (ctx.INTWORD() != null) {
            return this.int32Type;
        }
        if (ctx.BOOLEANWORD() != null) {
            return this.boolType;
        }
        if (ctx.CHARWORD() != null) {
            return this.charType;
        }
        if (ctx.STRINGWORD() != null) {
            return this.stringPtrType;
        }
        throw new IllegalStateException("Tipo no soportado: " + ctx.getText());
    }

    @Override
    public Object visitVarSingleDeclaration(ALPHAcompiladorParser.VarSingleDeclarationContext ctx) {
        LLVMTypeRef type = (LLVMTypeRef) visit(ctx.typeDenoter());

        LLVMValueRef storage;
        if (isInsideFunction()) {
            storage = LLVMBuildAlloca(this.builder, type, ctx.IDENTIFIER().getText());
        } else {
            storage = LLVMAddGlobal(this.module, type, ctx.IDENTIFIER().getText());
            LLVMSetInitializer(storage, defaultValueFor(type));
        }

        ctx.valorLLVM = storage;
        putVariable(ctx.IDENTIFIER().getText(), new VariableInfo(storage, type));
        return null;
    }

    @Override
    public Object visitVarSingleDeclarationAx(ALPHAcompiladorParser.VarSingleDeclarationAxContext ctx) {
        return visit(ctx.varSingleDeclaration());
    }

    @Override
    public Object visitWhileSingleCommand(ALPHAcompiladorParser.WhileSingleCommandContext ctx) {
        LLVMValueRef currentFunction = LLVMGetBasicBlockParent(LLVMGetInsertBlock(this.builder));
        LLVMBasicBlockRef condBlock = LLVMAppendBasicBlock(currentFunction, "while.cond");
        LLVMBasicBlockRef bodyBlock = LLVMAppendBasicBlock(currentFunction, "while.body");
        LLVMBasicBlockRef endBlock = LLVMAppendBasicBlock(currentFunction, "while.end");

        if (!hasTerminatorInCurrentBlock()) {
            LLVMBuildBr(this.builder, condBlock);
        }

        LLVMPositionBuilderAtEnd(this.builder, condBlock);
        LLVMValueRef condition = ensureI1((LLVMValueRef) visit(ctx.expression()));
        LLVMBuildCondBr(this.builder, condition, bodyBlock, endBlock);

        LLVMPositionBuilderAtEnd(this.builder, bodyBlock);
        visit(ctx.singleCommand());
        if (!hasTerminatorInCurrentBlock()) {
            LLVMBuildBr(this.builder, condBlock);
        }

        LLVMPositionBuilderAtEnd(this.builder, endBlock);
        return null;
    }

    private void compileLlToExe(Path llPath, Path exePath) {
        ProcessBuilder processBuilder = new ProcessBuilder("clang", llPath.toString(), "-o", exePath.toString());
        processBuilder.redirectErrorStream(true);
        try {
            Process process = processBuilder.start();
            String output = new String(process.getInputStream().readAllBytes());
            int exitCode = process.waitFor();
            if (exitCode != 0) {
                throw new RuntimeException("Error al compilar a exe con clang: " + output);
            }
        } catch (IOException e) {
            throw new RuntimeException("No se encontro clang en PATH para generar exe.", e);
        } catch (InterruptedException e) {
            Thread.currentThread().interrupt();
            throw new RuntimeException("Proceso de compilacion interrumpido.", e);
        }
    }

    private LLVMValueRef applyBinaryOperator(ALPHAcompiladorParser.OperatorContext opCtx, LLVMValueRef left, LLVMValueRef right) {
        Token op = getOperatorToken(opCtx);
        return switch (op.getType()) {
            case ALPHAcompiladorParser.ADD -> {
                if (isSameType(LLVMTypeOf(left), this.stringPtrType) && isSameType(LLVMTypeOf(right), this.stringPtrType)) {
                    yield buildStringConcat(left, right);
                }
                yield LLVMBuildAdd(this.builder, left, right, "addtmp");
            }
            case ALPHAcompiladorParser.SUB -> LLVMBuildSub(this.builder, left, right, "subtmp");
            case ALPHAcompiladorParser.MUL -> LLVMBuildMul(this.builder, left, right, "multmp");
            case ALPHAcompiladorParser.DIV -> LLVMBuildSDiv(this.builder, left, right, "divtmp");
            case ALPHAcompiladorParser.MOD -> LLVMBuildSRem(this.builder, left, right, "modtmp");
            case ALPHAcompiladorParser.EQEQ -> LLVMBuildICmp(this.builder, LLVMIntEQ, left, right, "eqtmp");
            case ALPHAcompiladorParser.NOTEQ -> LLVMBuildICmp(this.builder, LLVMIntNE, left, right, "netmp");
            case ALPHAcompiladorParser.LESS -> LLVMBuildICmp(this.builder, LLVMIntSLT, left, right, "lttmp");
            case ALPHAcompiladorParser.MORET -> LLVMBuildICmp(this.builder, LLVMIntSGT, left, right, "gttmp");
            case ALPHAcompiladorParser.LESSEQ -> LLVMBuildICmp(this.builder, LLVMIntSLE, left, right, "letmp");
            case ALPHAcompiladorParser.MOREEQ -> LLVMBuildICmp(this.builder, LLVMIntSGE, left, right, "getmp");
            default -> throw new IllegalStateException("Operador no soportado: " + op.getText());
        };
    }

    private Token getOperatorToken(ALPHAcompiladorParser.OperatorContext ctx) {
        if (ctx.ADD() != null) {
            return ctx.ADD().getSymbol();
        }
        if (ctx.SUB() != null) {
            return ctx.SUB().getSymbol();
        }
        if (ctx.MUL() != null) {
            return ctx.MUL().getSymbol();
        }
        if (ctx.DIV() != null) {
            return ctx.DIV().getSymbol();
        }
        if (ctx.MOD() != null) {
            return ctx.MOD().getSymbol();
        }
        if (ctx.EQEQ() != null) {
            return ctx.EQEQ().getSymbol();
        }
        if (ctx.NOTEQ() != null) {
            return ctx.NOTEQ().getSymbol();
        }
        if (ctx.LESS() != null) {
            return ctx.LESS().getSymbol();
        }
        if (ctx.MORET() != null) {
            return ctx.MORET().getSymbol();
        }
        if (ctx.LESSEQ() != null) {
            return ctx.LESSEQ().getSymbol();
        }
        if (ctx.MOREEQ() != null) {
            return ctx.MOREEQ().getSymbol();
        }
        throw new IllegalStateException("Operador invalido");
    }

    private void emitPrint(LLVMValueRef value) {
        LLVMValueRef printfFunction = ensurePrintfDeclaration();
        LLVMTypeRef valueType = LLVMTypeOf(value);

        String format;
        LLVMValueRef printValue = value;
        if (isSameType(valueType, this.int32Type)) {
            format = "%d\\n";
        } else if (isSameType(valueType, this.boolType)) {
            format = "%d\\n";
            printValue = LLVMBuildZExt(this.builder, value, this.int32Type, "booltoi32");
        } else if (isSameType(valueType, this.charType)) {
            format = "%c\\n";
            printValue = LLVMBuildSExt(this.builder, value, this.int32Type, "chartoi32");
        } else if (isSameType(valueType, this.stringPtrType)) {
            format = "%s\\n";
        } else {
            throw new IllegalStateException("print no soporta tipo: " + LLVMPrintTypeToString(valueType).getString());
        }

        LLVMValueRef formatString = LLVMBuildGlobalStringPtr(this.builder, format, "fmt");
        LLVMValueRef[] argsArray = new LLVMValueRef[]{formatString, printValue};
        PointerPointer<LLVMValueRef> args = new PointerPointer<>(argsArray);
        LLVMTypeRef printfType = this.methodTypes.get("printf");
        if (printfType == null) {
            throw new IllegalStateException("Tipo de printf no encontrado");
        }
        LLVMBuildCall2(this.builder, printfType, printfFunction, args, 2, "");
    }

    private LLVMValueRef ensurePrintfDeclaration() {
        LLVMValueRef current = LLVMGetNamedFunction(this.module, "printf");
        if (current != null) {
            if (!this.methodTypes.containsKey("printf")) {
                LLVMTypeRef[] printfArgs = {this.stringPtrType};
                LLVMTypeRef printfType = LLVMFunctionType(this.int32Type, new PointerPointer<>(printfArgs), 1, 1);
                this.methodTypes.put("printf", printfType);
            }
            return current;
        }

        LLVMTypeRef[] printfArgs = {this.stringPtrType};
        LLVMTypeRef printfType = LLVMFunctionType(this.int32Type, new PointerPointer<>(printfArgs), 1, 1);
        LLVMValueRef printfFunction = LLVMAddFunction(this.module, "printf", printfType);
        this.methodTypes.put("printf", printfType);
        return printfFunction;
    }

    private LLVMValueRef ensureI1(LLVMValueRef value) {
        LLVMTypeRef valueType = LLVMTypeOf(value);
        if (isSameType(valueType, this.boolType)) {
            return value;
        }
        return LLVMBuildICmp(this.builder, LLVMIntNE, value, defaultValueFor(valueType), "tobool");
    }

    private LLVMValueRef defaultValueFor(LLVMTypeRef type) {
        int kind = LLVMGetTypeKind(type);
        if (kind == LLVMIntegerTypeKind) {
            return LLVMConstInt(type, 0, 0);
        }
        if (kind == LLVMPointerTypeKind) {
            return LLVMConstPointerNull(type);
        }
        throw new IllegalStateException("No hay valor por defecto para tipo kind=" + kind);
    }

    private LLVMValueRef castValueIfNeeded(LLVMValueRef value, LLVMTypeRef expectedType) {
        LLVMTypeRef valueType = LLVMTypeOf(value);
        if (isSameType(valueType, expectedType)) {
            return value;
        }
        if (isSameType(valueType, this.boolType) && isSameType(expectedType, this.int32Type)) {
            return LLVMBuildZExt(this.builder, value, this.int32Type, "booltoint");
        }
        if (isSameType(valueType, this.charType) && isSameType(expectedType, this.int32Type)) {
            return LLVMBuildSExt(this.builder, value, this.int32Type, "chartoint");
        }
        throw new IllegalStateException("No se puede castear de " + LLVMPrintTypeToString(valueType).getString() + " a " + LLVMPrintTypeToString(expectedType).getString());
    }

    private boolean isSameType(LLVMTypeRef a, LLVMTypeRef b) {
        return LLVMPrintTypeToString(a).getString().equals(LLVMPrintTypeToString(b).getString());
    }

    private void pushScope() {
        this.variableScopes.push(new HashMap<>());
    }

    private void popScope() {
        this.variableScopes.pop();
    }

    private void putVariable(String name, VariableInfo info) {
        if (this.variableScopes.isEmpty()) {
            pushScope();
        }
        this.variableScopes.peek().put(name, info);
    }

    private VariableInfo resolveVariable(String name) {
        for (Map<String, VariableInfo> scope : this.variableScopes) {
            if (scope.containsKey(name)) {
                return scope.get(name);
            }
        }
        return null;
    }

    private boolean hasTerminatorInCurrentBlock() {
        LLVMBasicBlockRef currentBlock = LLVMGetInsertBlock(this.builder);
        if (currentBlock == null) {
            return false;
        }
        return LLVMGetBasicBlockTerminator(currentBlock) != null;
    }

    private boolean isInsideFunction() {
        LLVMBasicBlockRef currentBlock = LLVMGetInsertBlock(this.builder);
        if (currentBlock == null) {
            return false;
        }
        return LLVMGetBasicBlockParent(currentBlock) != null;
    }

    private char decodeCharLiteral(String raw) {
        String content = raw.substring(1, raw.length() - 1);
        if ("\\n".equals(content)) {
            return '\n';
        }
        if ("\\t".equals(content)) {
            return '\t';
        }
        if ("\\r".equals(content)) {
            return '\r';
        }
        if ("\\'".equals(content)) {
            return '\'';
        }
        if ("\\\\".equals(content)) {
            return '\\';
        }
        return content.charAt(0);
    }

    private String decodeStringLiteral(String raw) {
        String content = raw.substring(1, raw.length() - 1);
        return content
                .replace("\\\\", "\\")
                .replace("\\n", "\n")
                .replace("\\t", "\t")
                .replace("\\r", "\r")
                .replace("\\\"", "\"");
    }

    private LLVMValueRef buildStringConcat(LLVMValueRef left, LLVMValueRef right) {
        LLVMValueRef strlenFunction = ensureStrlenDeclaration();
        LLVMValueRef mallocFunction = ensureMallocDeclaration();
        LLVMValueRef strcpyFunction = ensureStrcpyDeclaration();
        LLVMValueRef strcatFunction = ensureStrcatDeclaration();

        LLVMTypeRef strlenType = LLVMFunctionType(LLVMInt64Type(), new PointerPointer<>(new LLVMTypeRef[]{this.stringPtrType}), 1, 0);
        LLVMTypeRef mallocType = LLVMFunctionType(this.stringPtrType, new PointerPointer<>(new LLVMTypeRef[]{LLVMInt64Type()}), 1, 0);
        LLVMTypeRef strcpyType = LLVMFunctionType(this.stringPtrType, new PointerPointer<>(new LLVMTypeRef[]{this.stringPtrType, this.stringPtrType}), 2, 0);
        LLVMTypeRef strcatType = LLVMFunctionType(this.stringPtrType, new PointerPointer<>(new LLVMTypeRef[]{this.stringPtrType, this.stringPtrType}), 2, 0);

        LLVMValueRef leftLen = LLVMBuildCall2(
                this.builder,
                strlenType,
                strlenFunction,
                new PointerPointer<>(new LLVMValueRef[]{left}),
                1,
                "left_len"
        );
        LLVMValueRef rightLen = LLVMBuildCall2(
                this.builder,
                strlenType,
                strlenFunction,
                new PointerPointer<>(new LLVMValueRef[]{right}),
                1,
                "right_len"
        );

        LLVMValueRef totalLen = LLVMBuildAdd(this.builder, leftLen, rightLen, "concat_len");
        LLVMValueRef totalLenWithNull = LLVMBuildAdd(this.builder, totalLen, LLVMConstInt(LLVMInt64Type(), 1, 0), "concat_len_null");

        LLVMValueRef destination = LLVMBuildCall2(
                this.builder,
                mallocType,
                mallocFunction,
                new PointerPointer<>(new LLVMValueRef[]{totalLenWithNull}),
                1,
                "concat_dst"
        );

        LLVMBuildCall2(
                this.builder,
                strcpyType,
                strcpyFunction,
                new PointerPointer<>(new LLVMValueRef[]{destination, left}),
                2,
                ""
        );

        LLVMBuildCall2(
                this.builder,
                strcatType,
                strcatFunction,
                new PointerPointer<>(new LLVMValueRef[]{destination, right}),
                2,
                ""
        );

        return destination;
    }

    private LLVMValueRef ensureStrlenDeclaration() {
        LLVMValueRef function = LLVMGetNamedFunction(this.module, "strlen");
        if (function != null) {
            return function;
        }
        LLVMTypeRef[] argTypes = {this.stringPtrType};
        LLVMTypeRef functionType = LLVMFunctionType(LLVMInt64Type(), new PointerPointer<>(argTypes), 1, 0);
        return LLVMAddFunction(this.module, "strlen", functionType);
    }

    private LLVMValueRef ensureMallocDeclaration() {
        LLVMValueRef function = LLVMGetNamedFunction(this.module, "malloc");
        if (function != null) {
            return function;
        }
        LLVMTypeRef[] argTypes = {LLVMInt64Type()};
        LLVMTypeRef functionType = LLVMFunctionType(this.stringPtrType, new PointerPointer<>(argTypes), 1, 0);
        return LLVMAddFunction(this.module, "malloc", functionType);
    }

    private LLVMValueRef ensureStrcpyDeclaration() {
        LLVMValueRef function = LLVMGetNamedFunction(this.module, "strcpy");
        if (function != null) {
            return function;
        }
        LLVMTypeRef[] argTypes = {this.stringPtrType, this.stringPtrType};
        LLVMTypeRef functionType = LLVMFunctionType(this.stringPtrType, new PointerPointer<>(argTypes), 2, 0);
        return LLVMAddFunction(this.module, "strcpy", functionType);
    }

    private LLVMValueRef ensureStrcatDeclaration() {
        LLVMValueRef function = LLVMGetNamedFunction(this.module, "strcat");
        if (function != null) {
            return function;
        }
        LLVMTypeRef[] argTypes = {this.stringPtrType, this.stringPtrType};
        LLVMTypeRef functionType = LLVMFunctionType(this.stringPtrType, new PointerPointer<>(argTypes), 2, 0);
        return LLVMAddFunction(this.module, "strcat", functionType);
    }

    private LLVMValueRef createModuleGlobalStringPtr(String literal) {
        LLVMValueRef stringConst = LLVMConstString(literal, literal.length(), 0);
        LLVMTypeRef stringType = LLVMTypeOf(stringConst);

        String globalName = "__str_global_" + this.globalStringCounter++;
        LLVMValueRef global = LLVMAddGlobal(this.module, stringType, globalName);
        LLVMSetInitializer(global, stringConst);
        LLVMSetGlobalConstant(global, 1);
        LLVMSetLinkage(global, LLVMPrivateLinkage);

        LLVMValueRef zero = LLVMConstInt(this.int32Type, 0, 0);
        PointerPointer<LLVMValueRef> indices = new PointerPointer<>(2);
        indices.put(0, zero);
        indices.put(1, zero);
        return LLVMConstInBoundsGEP2(stringType, global, indices, 2);
    }
}
