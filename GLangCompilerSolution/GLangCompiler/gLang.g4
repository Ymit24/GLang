grammar gLang;

program
	: header_statement* (function_declaration | struct_definition)*;

header_statement
	: EXTERN SYMBOL_NAME (COLON function_parameter_decl? (COMMA function_parameter_decl)*)? function_return_type?
	;

function_declaration
	: HASH SYMBOL_NAME COLON function_parameter_decl? (COMMA function_parameter_decl)* function_return_type? COLON? statement_block
	;

function_parameter_decl
	: LPAREN SYMBOL_NAME COLON datatype RPAREN
	;

function_return_type
	: ARROW datatype 
	;

struct_definition
    : PERCENT SYMBOL_NAME COLON function_parameter_decl (COMMA function_parameter_decl)*
    ;

statement_block
	: statement*
	;

statement
	: function_call
	| variable_assignment
	| if_statement
	| while_statement
	| for_statement
	| break_stmt
	| continue_stmt
	| return_stmt
	;

while_statement
	: WHILE LPAREN logical_expression RPAREN COLON statement_block END
	;

for_statement
	: FOR LPAREN for_initial? COMMA logical_expression? COMMA for_incrementor? RPAREN COLON statement_block END
	;

for_initial : variable_assignment;
for_incrementor : variable_assignment;

break_stmt
	: BREAK
	;

continue_stmt
	: CONTINUE
	;

if_statement
	: IF LPAREN logical_expression RPAREN COLON statement_block else_if* else_stmt? END
	;

else_if
	: ELSEIF LPAREN logical_expression RPAREN COLON statement_block
	;
else_stmt
	: ELSE COLON statement_block
	;

variable_assignment
	: SYMBOL_NAME COLON datatype EQUALS (expression | NULL_OP) #VariableDecl
	| SYMBOL_NAME EQUALS expression #VariableStdAssign
	| DOLLAR SYMBOL_NAME EQUALS expression #VariableDerefAssign
	| DOLLAR LPAREN expression RPAREN EQUALS expression #VariableDerefExprAssign
	| SYMBOL_NAME PLUS PLUS #VariableIncrementAssign
	| SYMBOL_NAME MINUS MINUS #VariableDecrementAssign
	;

function_call
	: LEFT_BRACKET SYMBOL_NAME function_arguments? RIGHT_BRACKET
	;

function_arguments
	: expression (COMMA expression)*;

return_stmt	: RETURN expression?;

logical_expression
	: logical_expression AND logical_expression #AndExpr
	| logical_expression OR logical_expression #OrExpr
	| logical_expression LEQ logical_expression #LEQExpr
	| logical_expression LSS logical_expression #LSSExpr
	| logical_expression GEQ logical_expression #GEQExpr
	| logical_expression GTR logical_expression #GTRExpr
	| logical_expression EQEQ logical_expression #EqEqExpr
	| logical_expression NEQ logical_expression #NeqExpr
	| LPAREN logical_expression RPAREN #ParenLogicExpr
	| expression #LogicExprLiteral
	;

expression
	: MINUS expression #NegateExpr
	| expression ASTERIK expression #MulExpr
	| expression FSLASH expression #DivExpr
	| expression PLUS expression #AddExpr
	| expression MINUS expression #SubExpr
	| expression ARROW datatype #CastExpr
	| LPAREN expression RPAREN #ParenExpr
	| function_call #FuncCallExpr
	| DOLLAR LPAREN expression RPAREN #DefrefExpr
	| DOLLAR SYMBOL_NAME #DefrefSymbolLiteral
	| AT SYMBOL_NAME #RefLiteral
	| SYMBOL_NAME PLUS PLUS #PostIncrementLiteral
	| SYMBOL_NAME MINUS MINUS #PostDecrementLiteral
	| PLUS PLUS SYMBOL_NAME #PreIncrementLiteral
	| MINUS MINUS SYMBOL_NAME #PreDecrementLiteral
	| NUMBER #NumberLiteral
	| SYMBOL_NAME #SymbolLiteral
	| STRING #StringLiteral
	;

datatype : SYMBOL_NAME (LPAREN NUMBER RPAREN)? ASTERIK?;

LINE_COMMENT
   : '//' ~[\r\n]* (EOF | '\r'? '\n') -> skip
   ;
WS : (' '|'\t'|'\r'|'\n')+ -> skip;
BLOCKCOMMENT : '/*' .*? '*/' -> skip;

fragment LOWERCASE : [a-z] ;
fragment UPPERCASE : [A-Z] ;

EXTERN   : 'extern';
RETURN   : 'ret';
IF       : 'if';
ELSEIF   : 'elseif';
ELSE     : 'else';
WHILE    : 'while';
FOR      : 'for';
END      : 'end';
BREAK    : 'break';
CONTINUE : 'continue';

AND    : '&&';
OR     : '|';

COMMA : ',';

SYMBOL_NAME : (LOWERCASE | UPPERCASE) (LOWERCASE | UPPERCASE | [0-9] | '_')*;

ARROW : '->';


// Conditional operators
LEQ : '<=';
LSS : '<';
GEQ : '>=';
GTR : '>';
EQEQ : '==';
NEQ : '!=';

HASH : '#';
COLON : ':';
EQUALS : '=';
PLUS : '+';
MINUS : '-';
ASTERIK : '*';
FSLASH : '/';
DOLLAR : '$';
AT : '@';
PERCENT : '%';

LEFT_BRACKET : '[';
RIGHT_BRACKET: ']';
LPAREN : '(';
RPAREN : ')';
NULL_OP : '_';

STRING : '"' .*? '"';

NUMBER
	: ( [0-9]* '.' )? [0-9]+;
