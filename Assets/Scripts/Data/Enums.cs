using System;

namespace GridPuzzle.Data
{
    public enum ColorType
    {
        None,
        Color_1,
        Color_2,
        Color_3,
        Color_4,
        Color_5
    }

    public enum VehicleState
    {
        InQueue,
        Moving,
        InStorage,
        Completed
    }

    [Flags]
    public enum ExposeDirection
    {
        None = 0,
        Top = 1,
        Right = 2,
        Bottom = 4,
        Left = 8
    }
}
