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
	| return_stmt
	;

variable_assignment
	: SYMBOL_NAME COLON DATATYPE EQUALS expression #VaraibleDecl
	| DOLLAR? SYMBOL_NAME EQUALS expression #VariableAssign
	;

function_call
	: LEFT_BRACKET SYMBOL_NAME function_arguments? RIGHT_BRACKET
	;

function_arguments
	: expression (COMMA expression)*;

return_stmt	: RETURN expression?;

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

WS : (' '|'\t'|'\r'|'\n')+ -> skip;
BLOCKCOMMENT : '/*' .*? '*/' -> skip;

fragment LOWERCASE : [a-z] ;
fragment UPPERCASE : [A-Z] ;

EXTERN : 'extern';
RETURN : 'ret';

COMMA : ',';

DATATYPE : ('i') ('8' | '16' | '32') ASTERIK?;
SYMBOL_NAME : (LOWERCASE | UPPERCASE) (LOWERCASE | UPPERCASE | [0-9] | '_')*;

ARROW : '->';

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
