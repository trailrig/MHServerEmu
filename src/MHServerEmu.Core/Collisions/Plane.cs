﻿using MHServerEmu.Core.VectorMath;

namespace MHServerEmu.Core.Collisions
{
    public struct Plane
    {
        public enum IntersectionType
        {
            Back,
            Front,
            Intersect
        }

        public Vector3 Normal;
        public float D;

        public Plane(Vector3 normal, float d)
        {
            Normal = normal;
            D = d;
        }

        public Plane(Vector3 p0, Vector3 p1, Vector3 p2)
        {
            Normal = Vector3.Cross(p1 - p0, p2 - p0);
            D = Vector3.Dot(Normal, p0);
        }

        public Plane(Vector3 normal, Vector3 point)
        {
            Normal = normal;
            D = Vector3.Dot(Normal, point);
        }

        public IntersectionType Intersects(in Aabb bound)
        {
            IntersectionType[] intersection = new IntersectionType[8];
            Vector3[] corners = bound.GetCorners();

            intersection[0] = Intersects(corners[0]);
            for (int i = 1; i < 8; i++)
            {
                intersection[i] = Intersects(corners[i]);
                if (intersection[i] != intersection[0])
                    return IntersectionType.Intersect;
            }

            return intersection[0];
        }

        public IntersectionType Intersects(in Vector3 point)
        {
            float distance = SignedDistanceToPoint(point);
            if (distance > 0.0f)
                return IntersectionType.Front;
            else if (distance < 0.0f)
                return IntersectionType.Back;
            else
                return IntersectionType.Intersect;
        }

        public float SignedDistanceToPoint(in Vector3 point)
        {
            return Vector3.Dot(point, Normal) - D;
        }

        public float SolveForZ(float x, float y)
        {
            if (Normal.Z != 0.0f)
                return (D - (Normal.X * x + Normal.Y * y)) / Normal.Z;
            else
                return 0.0f;
        }

    }
}
