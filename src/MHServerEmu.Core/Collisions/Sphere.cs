﻿using System.Text;
using MHServerEmu.Core.Helpers;
using MHServerEmu.Core.VectorMath;

namespace MHServerEmu.Core.Collisions
{
    public struct Sphere : IBounds
    {
        public Vector3 Center;
        public float Radius;
        public float RadiusSquared => Radius * Radius;
        public static Sphere Zero { get; } = new(Vector3.Zero, 0.0f);

        public Sphere(Vector3 center, float radius)
        {
            Center = center;
            Radius = radius;
        }

        public Aabb ToAabb()
        {
            return new (new Vector3 (Center.X - Radius, Center.Y - Radius, Center.Z - Radius),
                        new Vector3 (Center.X + Radius, Center.Y + Radius, Center.Z + Radius));
        }

        public bool Intersects(in Vector3 v)
        {
            return Vector3.LengthSqr(Center - v) <= RadiusSquared;
        }

        public ContainmentType Contains(in Vector3 point)
        {
            if (Vector3.LengthSqr(Center - point) <= RadiusSquared)
                return ContainmentType.Contains;
            else
                return ContainmentType.Disjoint;
        }

        public ContainmentType Contains(in Aabb2 bounds)
        {
            float radius = Radius;
            Vector3 center = Center;
            float minSq = 0.0f;
            float maxSq;

            float min = bounds.Min.X - center.X;
            float max = bounds.Max.X - center.X;
            if (min >= 0.0f)
            {
                if (min > radius) return ContainmentType.Disjoint;
                minSq = min * min;
                maxSq = max * max;
            }
            else if (max <= 0.0f)
            {
                if (max < -radius) return ContainmentType.Disjoint;
                minSq = max * max;
                maxSq = min * min;
            }
            else
            {
                maxSq = MathF.Max(max * max, min * min);
            }

            min = bounds.Min.Y - center.Y;
            max = bounds.Max.Y - center.Y;
            if (min >= 0.0f)
            {
                if (min > radius) return ContainmentType.Disjoint;
                minSq += min * min;
                maxSq += max * max;
            }
            else if (max <= 0.0f)
            {
                if (max < -radius) return ContainmentType.Disjoint;
                minSq += max * max;
                maxSq += min * min;
            }
            else
            {
                maxSq += MathF.Max(max * max, min * min);
            }

            float radiusSq = RadiusSquared;
            if (minSq > radiusSq) return ContainmentType.Disjoint;
            return maxSq <= radiusSq ? ContainmentType.Contains : ContainmentType.Intersects;
        }

        public bool Intersects(in Aabb bounds)
        {
            float sphereRadius = Radius;
            Vector3 center = Center;
            float minSq = 0.0f;

            for (int i = 0; i < 3; i++)
            {
                float min = bounds.Min[i] - center[i];
                float max = bounds.Max[i] - center[i];
                if (min >= 0.0f)
                {
                    if (min > sphereRadius) return false;
                    minSq += min * min;
                }
                else if (max <= 0.0f)
                {
                    if (max < -sphereRadius) return false;
                    minSq += max * max;
                }
            }

            return minSq < RadiusSquared;
        }

        public bool Intersects(in Obb obb)
        {
            return obb.Intersects(this);
        }

        public bool Intersects(in Sphere sphere)
        {
            return Vector3.Length(sphere.Center - Center) <= sphere.Radius + Radius;
        }

        public bool Intersects(in Capsule capsule)
        {
            return capsule.Intersects(this);
        }

        public bool Intersects(in Triangle triangle)
        {
            return triangle.Intersects(this);
        }

        public bool Sweep(in Aabb aabb, in Vector3 velocity, ref float time)
        {
            // Real-Time Collision Detection p.229 (IntersectMovingSphereAABB)
            // Compute the AABB resulting from expanding b by sphere radius r
            float diameter = Radius * 2.0f;
            Aabb expandedAabb = new(aabb.Center, aabb.Width + diameter, aabb.Length + diameter, aabb.Height + diameter);

            // Intersect ray against expanded AABB e. Exit with no intersection if ray
            // misses e, else get intersection point p and time t as result
            if (expandedAabb.IntersectRay(Center, velocity, ref time, out Vector3 point) == false) return false;

            // Compute which min and max faces of b the intersection point p lies
            // outside of. Note, u and v cannot have the same bits set and
            // they must have at least one bit set among them
            int u = 0, v = 0;
            if (point.X < aabb.Min.X) u |= 1;
            if (point.X > aabb.Max.X) v |= 1;
            if (point.Y < aabb.Min.Y) u |= 2;
            if (point.Y > aabb.Max.Y) v |= 2;
            if (point.Z < aabb.Min.Z) u |= 4;
            if (point.Z > aabb.Max.Z) v |= 4;

            // 'Or' all set bits together into a bit mask (note: here u + v == u | v)
            int m = u + v;
            // Define line segment [c, c+d] specified by the sphere movement
            Segment seg = new(Center, Center + velocity);
            // If all 3 bits set (m == 7) then p is in a vertex region
            if (m == 7)
            {   // Must now intersect segment [c, c+d] against the capsules of the three
                // edges meeting at the vertex and return the best time, if one or more hit
                float tmin = float.MaxValue;
                if (Capsule.IntersectsSegment(seg, SweepGetCorner(aabb, v), SweepGetCorner(aabb, v ^ 1), Radius, ref time))
                    tmin = Math.Min(time, tmin);
                if (Capsule.IntersectsSegment(seg, SweepGetCorner(aabb, v), SweepGetCorner(aabb, v ^ 2), Radius, ref time))
                    tmin = Math.Min(time, tmin);
                if (Capsule.IntersectsSegment(seg, SweepGetCorner(aabb, v), SweepGetCorner(aabb, v ^ 4), Radius, ref time))
                    tmin = Math.Min(time, tmin);
                if (tmin == float.MaxValue)
                    return false; // No intersection
                time = tmin;
                return true;
            }
            // If only one bit set in m, then p is in a face region
            if ((m & m - 1) == 0) return true; // Do nothing. Time t from intersection with expanded box is correct intersection time
            // p is in an edge region. Intersect against the capsule at the edge
            return Capsule.IntersectsSegment(seg, SweepGetCorner(aabb, u ^ 7), SweepGetCorner(aabb, v), Radius, ref time);
        }

        private static Vector3 SweepGetCorner(in Aabb b, int n)
        {
            // Support function that returns the AABB vertex with index n
            return new Vector3((n & 1) != 0 ? b.Max.X : b.Min.X,
                               (n & 2) != 0 ? b.Max.Y : b.Min.Y,
                               (n & 4) != 0 ? b.Max.Z : b.Min.Z);
        }

        public bool Sweep(in Obb obb, Vector3 velocity, ref float time)
        {
            Vector3 obbVelocity = obb.TransformVector(velocity);
            Vector3 center = obb.TransformPoint(Center);
            Sphere sphere = new(center, Radius);
            Aabb aabb = new(obb.Center - obb.Extents, obb.Center + obb.Extents);
            return sphere.Sweep(aabb, obbVelocity, ref time);
        }

        public bool Intersects(in Segment seg, ref float time)
        {
            return Intersects(seg, ref time, out _);
        }

        public bool Intersects(in Segment segment, ref float time, out Vector3 intersectionPoint)
        {
            Vector3 direction = segment.Direction;
            float length = Vector3.Length(direction);
            Vector3 directionNorm = Vector3.Normalize(direction);

            return IntersectsSegment(segment.Start, directionNorm, length, Center, Radius, out time, out intersectionPoint);
        }

        public static bool IntersectsSegment(in Vector3 start, in Vector3 directionNorm, float length, in Vector3 center, float radius, out float time, out Vector3 intersectionPoint)
        {
            if (IntersectsRay(start, directionNorm, center, radius, out float rayDistance, out Vector3 rayPoint))
            {
                Vector3 rayEdge = rayPoint - start;
                float distance = Vector3.Length(rayEdge);
                float rayTime = distance / length;
                if (rayTime <= 1.0f)
                {
                    time = rayTime;
                    intersectionPoint = rayPoint;
                    return true;
                }
            }

            time = 0.0f;
            intersectionPoint = Vector3.Zero;
            return false;
        }

        public static bool IntersectsRay(in Vector3 start, in Vector3 directionNorm, in Vector3 center, float radius, out float rayDistance, out Vector3 rayPoint)
        {
            if (IntersectsRay(start, directionNorm, center, radius, out rayDistance))
            {
                rayPoint = start + directionNorm * rayDistance;
                return true;
            }

            rayDistance = 0.0f;
            rayPoint = Vector3.Zero;
            return false;
        }

        public static bool IntersectsRay(in Vector3 start, in Vector3 directionNorm, in Vector3 center, float radius, out float rayDistance)
        {
            // Real-Time Collision Detection p.178 (IntersectRaySphere)
            Vector3 dir = start - center;
            float b = Vector3.Dot(dir, directionNorm);
            float c = Vector3.Dot(dir, dir) - radius * radius;
            // Exit if r’s origin outside s (c > 0) and r pointing away from s (b > 0)
            if (c > 0.0f && b > 0.0f)
            {
                rayDistance = 0.0f;
                return false;
            }

            float discreminant = b * b - c;
            // A negative discriminant corresponds to ray missing sphere
            if (discreminant > 0.0f)
            {   // Ray now found to intersect sphere, compute smallest t value of intersection                
                rayDistance = -b - MathHelper.SquareRoot(discreminant);
                // If t is negative, ray started inside sphere so clamp t to zero
                if (rayDistance < 0.0f) rayDistance = 0.0f;
                return true;
            }

            rayDistance = 0.0f;
            return false;
        }

        public static bool SweepSegment2d(in Vector3 segmentStart, in Vector3 segmentEnd, in Vector3 sphereStart, float sphereRadius, in Vector3 direction, float magnitude, ref float distanceToIntersect, SweepSegmentFlags segmentFlag)
        {
            Vector3 segment = segmentEnd - segmentStart;

            bool flip = Segment.Cross2D(segment, sphereStart - segmentStart) > 0.0f;
            Vector3 segmentPerp = flip ? Vector3.Perp2D(-segment) : Vector3.Perp2D(segment);

            Plane plane = new (Vector3.Normalize(segmentPerp), segmentStart);
            Vector3 planeNormal = plane.Normal;
            float planeDistance = Vector3.Dot(sphereStart, planeNormal) - plane.D;

            float sphereScalar = Vector3.Dot(segment, sphereStart - segmentStart) / Vector3.Dot(segment, segment);
            if ((Math.Abs(planeDistance) <= sphereRadius) && (sphereScalar >= 0.0f && sphereScalar <= 1.0f))
            {
                distanceToIntersect = 0.0f;
                return true;
            }

            float dotNormal = Vector3.Dot(direction, planeNormal);
            if (dotNormal * planeDistance >= 0.0f)
            {
                distanceToIntersect = 0.0f;
                return false;
            }

            float radiusAdjustment = (planeDistance > 0.0f) ? sphereRadius : -sphereRadius;
            float distance = (radiusAdjustment - planeDistance) / dotNormal;
            Vector3 spherePoint = sphereStart + direction * distance;
            Vector3 planePoint = spherePoint - planeNormal * radiusAdjustment;

            float planeScalar = Vector3.Dot(segment, planePoint - segmentStart) / Vector3.Dot(segment, segment);
            if (planeScalar >= 0.0f && planeScalar <= 1.0f)
            {
                distanceToIntersect = distance;
                return true;
            }
            else
            {
                if (segmentFlag != SweepSegmentFlags.Ignore)
                {
                    distanceToIntersect = float.MaxValue;
                    for (int i = 0; i < 2; ++i)
                    {
                        Vector3 point = (i == 0) ? segmentStart : segmentEnd;
                        if (IntersectsRay(sphereStart, direction, point, sphereRadius, out float rayDistance))
                            if (rayDistance < distanceToIntersect)
                                distanceToIntersect = rayDistance;
                    }
                    return distanceToIntersect <= magnitude;
                }
                else
                {
                    distanceToIntersect = 0.0f;
                    return false;
                }
            }
        }

        public override string ToString()
        {
            StringBuilder sb = new();
            sb.AppendLine($" Radius: {Radius}");
            //sb.AppendLine($"A: {Center.ToStringFloat()}");
            return sb.ToString();
        }

        public bool Sweep(in Sphere otherSphere, in Vector3 otherVelocity, in Vector3 velocity, ref float resultTime, Axis axis = Axis.Invalid)
        {
            // Real-Time Collision Detection p.224 (TestMovingSphereSphere)

            Vector3 s = otherSphere.Center - Center; // Vector between sphere centers
            Vector3 v = otherVelocity - velocity; // Relative motion of s1 with respect to stationary s0

            if (axis != Axis.Invalid)
            {
                s[(int)axis] = 0.0f;
                v[(int)axis] = 0.0f;
            }

            float r = otherSphere.Radius + Radius; // Sum of sphere radii
            float c = Vector3.Dot(s, s) - r * r;
            if (c < 0.0f)
            {   // Spheres initially overlapping so exit directly
                resultTime = 0.0f;
                return true;
            }
            float a = Vector3.Dot(v, v);
            if (a < Segment.Epsilon) return false; // Spheres not moving relative each other
            float b = Vector3.Dot(v, s);
            if (b >= 0.0f) return false; // Spheres not moving towards each other
            float d = b * b - a * c;
            if (d < 0.0f) return false; // No real-valued root, spheres do not intersect

            resultTime = (-b - MathHelper.SquareRoot(d)) / a;
            return resultTime >= 0.0f && resultTime <= 1.0f;
        }

    }

    public enum SweepSegmentFlags
    {
        None = 0,
        Ignore = 1,
    }
}
