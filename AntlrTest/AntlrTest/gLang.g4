grammar gLang;

program
	: header_statement* function_declaration+;

header_statement
	: EXTERN SYMBOL_NAME
	;

function_declaration
	: HASH SYMBOL_NAME COLON function_parameter_decl? (COMMA function_parameter_decl)* function_return_type? COLON? statement_block
	;

function_parameter_decl
	: LPAREN SYMBOL_NAME COLON DATATYPE RPAREN
	;

function_return_type
	: ARROW DATATYPE
	;

statement_block
	: statement*
	;

statement
	: function_call
	| variable_assignment
	| if_statement
	| while_statement
	| break_stmt
	| return_stmt
	;

while_statement
	: WHILE LPAREN logical_expression RPAREN COLON statement_block END
	;

break_stmt
	: BREAK
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
	: SYMBOL_NAME COLON DATATYPE EQUALS expression #VaraibleDecl
	| SYMBOL_NAME EQUALS expression #VaraibleStdAssign
	| DOLLAR SYMBOL_NAME EQUALS expression #VariableDerefAssign
	| DOLLAR LPAREN expression RPAREN EQUALS expression #VariableDerefExprAssign
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
	| LPAREN expression RPAREN #ParenExpr
	| function_call #FuncCallExpr
	| DOLLAR LPAREN expression RPAREN #DefrefExpr
	| DOLLAR SYMBOL_NAME #DefrefSymbolLiteral
	| AT SYMBOL_NAME #RefLiteral
	| NUMBER #NumberLiteral
	| SYMBOL_NAME #SymbolLiteral
	| STRING #StringLiteral
	;

LINE_COMMENT
   : '//' ~[\r\n]* (EOF | '\r'? '\n') -> skip
   ;
WS : (' '|'\t'|'\r'|'\n')+ -> skip;
BLOCKCOMMENT : '/*' .*? '*/' -> skip;

fragment LOWERCASE : [a-z] ;
fragment UPPERCASE : [A-Z] ;

EXTERN : 'extern';
RETURN : 'ret';
IF     : 'if';
ELSEIF : 'elseif';
ELSE   : 'else';
WHILE  : 'while';
END    : 'end';
BREAK  : 'break';

AND    : '&&';
OR     : '|';

COMMA : ',';

DATATYPE : ('i') ('8' | '16' | '32') ASTERIK?;
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

LEFT_BRACKET : '[';
RIGHT_BRACKET: ']';
LPAREN : '(';
RPAREN : ')';

STRING : '"' .*? '"';

NUMBER
	: ( [0-9]* '.' )? [0-9]+;
