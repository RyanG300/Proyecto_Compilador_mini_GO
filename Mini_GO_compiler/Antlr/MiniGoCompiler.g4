grammar MiniGoCompiler;

//------------------Lexer------------------
//------------------Lexer------------------
//------------------Lexer------------------

//Palabras reservadas
PACKAGE: 'package';
VAR: 'var';
TYPE: 'type';
FUNC: 'func';
STRUCT: 'struct'; 
APPEND: 'append';
LEN: 'len';
CAP: 'cap';
PRINT: 'print';
PRINTLN: 'println';
RETURN: 'return';
BREAK: 'break';
CONTINUE: 'continue';
IF : 'if';
ELSE : 'else';
FOR : 'for';
SWITCH : 'switch';
CASE : 'case';
DEFAULT : 'default';



//Simbolos reservados
SEMI: ';';
ASSIGN : '=';
COLON: ':';
COLONASSIGN : ':=';
LEFTP : '(';
RIGHTP : ')';
LEFTCORCHET : '[';
RIGTHCORCHET : ']'; 
LEFTBRACE : '{';
RIGHTBRACE : '}';
COMA: ',';
POINT: '.';
ADD : '+';
ADDONE : '++'; 
SUB : '-';
SUBONE : '--';
MUL : '*';
DIV : '/';
MOD : '%';
MULX : '<<';
DIVX : '>>';
AMPERSAND : '&';
BITCLEAR : '&^';
BITXOR : '^';
EQEQ : '==';
NOTEQ : '!=';
MORET : '>';
LESS : '<';
MOREEQ : '>=';
LESSEQ : '<=';
AND : '&&';
OR : '||';
BITOR : '|';
NOT : '!';
ADDASSIGN : '+=';
SUBASSIGN : '-=';
MULASSIGN : '*=';
DIVASSIGN : '/=';
XORASSIGN : '^=';
ORASSIGN : '|=';
AMPERSANDASSIGN : '&=';
MULXASSIGN : '<<=';
DIVXASSIGN : '>>=';
BITCLEARASSIGN : '&^=';
MODASSIGN : '%=';


//Others
IDENTIFIER: (UNDERSC|LETTER) (LETTER|UNDERSC|DIGIT)*;
INTLITERAL: DIGIT DIGIT*;
FLOATLITERAL: DIGIT* '.' DIGIT*;   
RAWSTRINGLITERAL: '`' .*? '`'; 
INTERPRETEDSTRINGLITERAL: '"' .*? '"';
RUNELITERAL: '\'' . '\'';

 
WS : [ \n\r\t] -> skip;
LINECOMMENT : '//' ~[\n\r]* -> skip;
COMMENT : '/*' .*? '*/' -> skip;

// fragment
fragment DIGIT : [0-9];
fragment LETTER : [a-zA-Z];
fragment UNDERSC : '_';


//------------------parser------------------
//------------------parser------------------
//------------------parser------------------

root: PACKAGE IDENTIFIER SEMI topDeclarationList EOF;
topDeclarationList: (variableDecl|typeDecl|funcDecl)*; 
variableDecl: VAR singleVarDecl SEMI
              | VAR LEFTP innerVarDecls RIGHTP SEMI
              | VAR LEFTP RIGHTP SEMI; 
innerVarDecls: singleVarDecl SEMI (singleVarDecl SEMI)*;
singleVarDecl: identifierList declType ASSIGN expressionList
              | identifierList ASSIGN expressionList
              | singleVarDeclNoExps;
singleVarDeclNoExps: identifierList declType;
typeDecl: TYPE singleTypeDecl SEMI
              | TYPE LEFTP innerTypeDecls RIGHTP SEMI
              | TYPE LEFTP RIGHTP SEMI;
innerTypeDecls: singleTypeDecl SEMI (singleTypeDecl SEMI)*; 
singleTypeDecl: IDENTIFIER declType; 
funcDecl: funcFrontDecl block SEMI;
funcFrontDecl: FUNC IDENTIFIER LEFTP funcArgDecls? RIGHTP declType?;
funcArgDecls: singleVarDeclNoExps (COMA singleVarDeclNoExps)*;
declType: LEFTP declType RIGHTP
          | IDENTIFIER                     
          | sliceDeclType
          | arrayDeclType
          | structDeclType;
sliceDeclType: LEFTCORCHET RIGTHCORCHET declType;
arrayDeclType: LEFTCORCHET INTLITERAL RIGTHCORCHET declType;
structDeclType: STRUCT LEFTBRACE structMemDecls? RIGHTBRACE;
structMemDecls: singleVarDeclNoExps SEMI (singleVarDeclNoExps SEMI)*;
identifierList: IDENTIFIER (COMA IDENTIFIER)*;
expression: primaryExpression
            | expression MUL expression
            | expression DIV expression
            | expression MOD expression
            | expression MULX expression
            | expression DIVX expression
            | expression AMPERSAND expression
            | expression BITCLEAR expression
            | expression ADD expression
            | expression SUB expression
            | expression BITOR expression
            | expression BITXOR expression
            | expression EQEQ expression
            | expression NOTEQ expression
            | expression LESS expression
            | expression LESSEQ expression
            | expression MORET expression
            | expression MOREEQ expression
            | expression AND expression
            | expression OR expression
            | ADD expression
            | SUB expression
            | NOT expression
            | BITXOR expression;
expressionList: expression (COMA expression)*;
primaryExpression: operand
                   | primaryExpression selector 
                   | primaryExpression index 
                   | primaryExpression arguments 
                   | appendExpression 
                   | lengthExpression
                   | capExpression;
operand: literal
         | IDENTIFIER
         | LEFTP expression RIGHTP;
literal: INTLITERAL								 
		 | FLOATLITERAL							 
	     | RUNELITERAL							 
		 | RAWSTRINGLITERAL						 
		 | INTERPRETEDSTRINGLITERAL;	
index: LEFTCORCHET expression RIGTHCORCHET;
arguments: LEFTP expressionList? RIGHTP;
selector:  POINT IDENTIFIER;
appendExpression: APPEND LEFTP expression COMA expression RIGHTP;
lengthExpression: LEN LEFTP expression RIGHTP;
capExpression: CAP LEFTP expression RIGHTP;
statementList: statement*;
block: LEFTBRACE statementList RIGHTBRACE;
statement: PRINT LEFTP expressionList? RIGHTP SEMI 
			| PRINTLN LEFTP expressionList? RIGHTP SEMI 
			| RETURN expression? SEMI 
			| BREAK SEMI 
			| CONTINUE SEMI
			| simpleStatement SEMI 
			| block SEMI
			| switch SEMI
			| ifStatement SEMI
			| loop SEMI
			| typeDecl
			| variableDecl;
simpleStatement: (expression (ADDONE|SUBONE)? 
                 | assignmentStatement
                 | expressionList COLONASSIGN expressionList)?;
assignmentStatement: expressionList ASSIGN expressionList 
			         |expression ADDASSIGN expression
			         |expression AMPERSANDASSIGN expression 
                     |expression SUBASSIGN expression
			         |expression ORASSIGN expression
			         |expression MULASSIGN expression 
			         |expression XORASSIGN expression 
			         |expression MULXASSIGN expression 
			         |expression DIVXASSIGN expression 
			         |expression BITCLEARASSIGN expression
			         |expression MODASSIGN expression
			         |expression DIVASSIGN expression;
ifStatement: IF expression block 
			| IF expression block ELSE ifStatement 
			| IF expression block ELSE block 
			| IF simpleStatement  SEMI expression block 
			| IF simpleStatement SEMI expression block ELSE ifStatement
			| IF simpleStatement  SEMI expression block ELSE block; 
loop: FOR block 
	  | FOR expression block 
      | FOR simpleStatement SEMI expression SEMI simpleStatement block
	  | FOR simpleStatement SEMI SEMI simpleStatement block;
switch: SWITCH simpleStatement SEMI expression LEFTBRACE expressionCaseClauseList RIGHTBRACE 
			| SWITCH expression LEFTBRACE expressionCaseClauseList RIGHTBRACE 
			| SWITCH simpleStatement SEMI LEFTBRACE expressionCaseClauseList RIGHTBRACE 
			| SWITCH LEFTBRACE expressionCaseClauseList RIGHTBRACE;  
expressionCaseClauseList: (expressionCaseClause expressionCaseClauseList)?;
expressionCaseClause: expressionSwitchCase COLON statementList; 
expressionSwitchCase : CASE expressionList 
			| DEFAULT;
 			

			 








