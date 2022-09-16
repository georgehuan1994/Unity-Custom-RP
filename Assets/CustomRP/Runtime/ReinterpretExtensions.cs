using System.Runtime.InteropServices;

public static class ReinterpretExtensions
{
    [StructLayout(LayoutKind.Explicit)]
    struct IntFloat
    {
        [FieldOffset(0)] public int intValue;
        [FieldOffset(0)] public float floatValue;
    }
    
    public static float ReinterpretAsFloat(this int value)
    {
        IntFloat converter = default;
        converter.intValue = value;
        return converter.floatValue;
    }

    // class MyClass
    // {
    //     private float _x = 0;
    //
    //     public int x
    //     {
    //         set
    //         {
    //             _x = (float)value;
    //         }
    //         get
    //         {
    //             return (int)_x;
    //         }
    //     }
    // }
}
