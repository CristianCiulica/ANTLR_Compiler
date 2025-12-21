grammar MiniLang;

INT: 'int';
FLOAT: 'float';
DOUBLE: 'double';
STRING_TYPE: 'string';
CONST: 'const';
VOID: 'void';
IF: 'if';
ELSE: 'else';
FOR: 'for';
WHILE: 'while';
RETURN: 'return';

PLUS: '+'; MINUS: '-'; MUL: '*'; DIV: '/'; MOD: '%';
LT: '<'; GT: '>'; LE: '<='; GE: '>='; EQ: '=='; NEQ: '!=';
AND: '&&'; OR: '||'; NOT: '!';
ASSIGN: '='; ADD_ASSIGN: '+='; SUB_ASSIGN: '-='; MUL_ASSIGN: '*='; DIV_ASSIGN: '/='; MOD_ASSIGN: '%=';
INC: '++'; DEC: '--';

LPAREN: '('; RPAREN: ')';
LBRACE: '{'; RBRACE: '}';
COMMA: ','; SEMI: ';';

IDENTIFIER: [a-zA-Z_][a-zA-Z0-9_]*;
INT_LITERAL: [0-9]+;
FLOAT_LITERAL: [0-9]+ '.' [0-9]+;
STRING_LITERAL: '"' .*? '"';

WS: [ \t\r\n]+ -> skip;
LINE_COMMENT: '//' ~[\r\n]* -> skip;
BLOCK_COMMENT: '/*' .*? '*/' -> skip;

program: globalDecl* EOF;

globalDecl
    : varDecl       # GlobalVarDeclaration
    | funcDecl      # GlobalFuncDeclaration
    ;

varDecl
    : CONST? type IDENTIFIER (ASSIGN expression)? SEMI
    ;

funcDecl
    : (type | VOID) IDENTIFIER LPAREN paramList? RPAREN block
    ;

paramList
    : param (COMMA param)*
    ;

param
    : type IDENTIFIER
    ;

block
    : LBRACE statement* RBRACE
    ;

statement
    : varDecl                                       # LocalVarStmt
    | IF LPAREN expression RPAREN block (ELSE block)? # IfStmt
    | WHILE LPAREN expression RPAREN block          # WhileStmt
    | FOR LPAREN (varDecl | (assignStmt | ) SEMI) expression? SEMI (assignStmt | expression | ) RPAREN block # ForStmt
    | RETURN expression? SEMI                       # ReturnStmt
    | expression SEMI                               # ExprStmt
    | block                                         # BlockStmt
    | SEMI                                          # EmptyStmt
    ;

assignStmt
    : IDENTIFIER (ASSIGN | ADD_ASSIGN | SUB_ASSIGN | MUL_ASSIGN | DIV_ASSIGN | MOD_ASSIGN) expression
    | IDENTIFIER (INC | DEC)
    ;

expression
    : LPAREN expression RPAREN                  # ParenExpr
    | NOT expression                            # NotExpr
    | expression (MUL | DIV | MOD) expression   # MulDivExpr
    | expression (PLUS | MINUS) expression      # AddSubExpr
    | expression (LT | GT | LE | GE) expression # RelationalExpr
    | expression (EQ | NEQ) expression          # EqualityExpr
    | expression AND expression                 # LogicAndExpr
    | expression OR expression                  # LogicOrExpr
    | IDENTIFIER LPAREN args? RPAREN            # FuncCallExpr
    | IDENTIFIER                                # IdExpr
    | INT_LITERAL                               # IntExpr
    | FLOAT_LITERAL                             # FloatExpr
    | STRING_LITERAL                            # StringExpr
    | assignStmt                                # AssignmentExpr
    ;

args: expression (COMMA expression)*;

type: INT | FLOAT | DOUBLE | STRING_TYPE;