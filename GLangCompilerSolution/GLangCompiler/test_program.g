extern printf

#test:(a:u16), (b: u32), (c: u8):
    [printf "A: %#04X %#04X %#04X\n", a, b, c]
    ret

#fib:(n:i32)->i32:
    if (n<=1):
        ret 1
    end
    ret [fib n-1] + [fib n-2]

#main:
    a:u8=0 // secret u32
    b:u8=8 // secret u32
    c:u8=8 // secret u32
    d:u16=8 // secret u32
    e:u8=8 // secret u32
    // We need 2 bytes of padding here
    f:u32=2
    g:u16=3 // secret u32
    [test a, f, f] // spooky
    [printf "Values: %#04X %#04X %#04X %#04X %#04X %#04X %#04X\n", a->u8, b, c, d, e, f, g]
    for (i:i32=0,i<20,i++):
        [printf "Fib of %d is %d\n", i, [fib i]]
    end
    ret 0
