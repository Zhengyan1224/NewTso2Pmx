using System;

namespace TDCG;

public static class Geometry
{
    public static float DegreeToRadian(float angle)
        => (float)(Math.PI * angle / 180.0);

    public static float RadianToDegree(float angle)
        => (float)(180.0 * angle / Math.PI);
}
