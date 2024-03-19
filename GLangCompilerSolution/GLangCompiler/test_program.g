extern printf

%example:(a:u8),(b:u8)


#takes_example:(e1:example):
    ret

#returns_example:(a:u8)->example:
    ret { example a: a, b: 0 }

#main:
    some_example : example = { example a: 10, b: 0 }
    ret 0

///*
//
//ESP ----
//-00 0 e.a
//-01 0 e.b
//-02 0 p
//-03 0 p
//-04 0
//-05 0
//-06 0
//-07 0
//-08 0
//-09 0
//-10 0
//-11 0
//-12 0
//-13 0
//-14 0
//-15 0
//-16 0
//-17 0
//-18 0
//-19 0
//*.
