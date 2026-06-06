grammar ALPHAcompilador;

//------------------Lexer------------------
//------------------Lexer------------------
//------------------Lexer------------------


//Palabras reervadas
IF : 'if';
THEN : 'then';
ELSE : 'else';
WHILE : 'while';
DO : 'do';
LET: 'let';
IN : 'in';
BEGIN : 'begin';
END : 'end';
CONST : 'const';
VAR : 'var';
VOID : 'void';
RETURN : 'return';
INTWORD : 'int';
STRINGWORD : 'string';
CHARWORD : 'char';
BOOLEANWORD : 'boolean';
TRUE : 'true';
FALSE : 'false';

//Simbolos
SEMI : ';';
ASSIGN : ':=';
LEFTP : '(';
RIGHTP : ')';
VIR : '~';
COLON : ':';
ADD : '+';
SUB : '-';
MUL : '*';
DIV : '/';
MOD : '%';
EQEQ : '==';
NOTEQ : '!=';
MORET : '>';
LESS : '<';
MOREEQ : '>=';
LESSEQ : '<=';
COMA : ',';

//Others
CHARLIT : '\'' . '\'';
STRINGLIT : '"' .*? '"' ;
IDENTIFIER : LETTER (LETTER|DIGIT)*;
LITERAL : DIGIT DIGIT*;
WS : [ \n\r\t] -> skip;
LINECOMMENT : '//' ~[\n\r]* -> skip;
COMMENT : '/*' .*? '*/' -> skip;


// fragment
fragment DIGIT : [0-9];
fragment LETTER : [a-zA-Z];


//------------------parser------------------
//------------------parser------------------
//------------------parser------------------
program : (singleCommand|command) EOF;
command : singleCommand (SEMI singleCommand)* SEMI;
singleCommand :
        identifier ASSIGN expression                                                  #assignSingleCommand
        | IDENTIFIER LEFTP argumentList? RIGHTP                                       #methodCallSingleCommand
        | IF expression THEN singleCommand (ELSE singleCommand)?                      #ifSingleCommand
        | WHILE expression DO singleCommand                                           #whileSingleCommand
        | LET declaration IN singleCommand                                            #letSingleCommand
        | BEGIN command END                                                           #blockSingleCommand
        | RETURN expression?                                                          #returnSingleCommand;
declaration  : (singleDeclaration | advanceDeclaration) (SEMI (singleDeclaration | advanceDeclaration))* SEMI;
advanceDeclaration: (VOID|typeDenoter) IDENTIFIER LEFTP paramList? RIGHTP
                    BEGIN command END;
paramList: param (COMA param)*;
param: typeDenoter IDENTIFIER;
argumentList: expression (COMA expression)*;
singleDeclaration :
           CONST IDENTIFIER VIR expression                                            #constSingleDeclaration
    	   | varSingleDeclaration                                                     #varSingleDeclarationAx;
varSingleDeclaration locals[org.bytedeco.llvm.LLVM.LLVMValueRef valorLLVM = null]: VAR IDENTIFIER COLON typeDenoter;
typeDenoter : IDENTIFIER|INTWORD|STRINGWORD|CHARWORD|BOOLEANWORD;
expression : primaryExpression (operator primaryExpression)*;
primaryExpression : SUB? LITERAL                                                      #numPrimaryExpression
                    | identifier                                                      #idPrimaryExpression
                    | STRINGLIT                                                       #stringPrimaryExpression
                    | CHARLIT                                                         #charPrimaryExpression
                    | TRUE                                                            #truePrimaryExpression
                    | FALSE                                                           #falsePrimaryExpression
                    | SUB? IDENTIFIER LEFTP argumentList? RIGHTP                      #methodCallPrimaryExpression
                    | SUB? LEFTP expression RIGHTP                                    #groupPrimaryExpression;
operator : ADD | SUB | MUL | DIV | MOD | EQEQ | NOTEQ | LESS | MORET | LESSEQ | MOREEQ;
identifier
locals [ParserRuleContext decl = null] : IDENTIFIER;