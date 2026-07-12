using System;
using UnityEngine;
using UnityEngine.AI;

namespace AccessibilityMod.Utils
{
    /// <summary>
    /// Answers "can the player actually walk there?" using the game's own NavMesh - the
    /// same data click-to-move pathfinding uses. Lets navigation announcements say
    /// "not reachable" up front instead of the player only finding out after an
    /// auto-walk fails. Returns null (unknown) whenever either endpoint can't be
    /// snapped onto the NavMesh, so callers can stay silent rather than guess wrong.
    /// </summary>
    public static class ReachabilityChecker
    {
        // How far an endpoint may be off the NavMesh and still count as "on it".
        // Interactables often sit on furniture/walls slightly above or beside the
        // walkable surface, so the target gets a little more slack than the player.
        private const float PLAYER_SNAP_RADIUS = 2.0f;
        private const float TARGET_SNAP_RADIUS = 3.0f;

        // Interactables count as reachable when auto-walk can get within interaction
        // range, not only when the NavMesh path ends exactly at them. Doors and wall
        // objects snap to the far side of walls, which made a strict PathComplete check
        // flag doors one metre away as unreachable (observed in the Whirling).
        private const float CLOSE_ENOUGH = 3.0f;

        public static bool? IsReachable(Vector3 playerPos, Vector3 targetPos)
        {
            try
            {
                // Anything already within interaction range is reachable by definition.
                if (Vector3.Distance(playerPos, targetPos) <= CLOSE_ENOUGH)
                {
                    return true;
                }

                if (!NavMesh.SamplePosition(playerPos, out var playerHit, PLAYER_SNAP_RADIUS, NavMesh.AllAreas))
                {
                    return null;
                }

                if (!NavMesh.SamplePosition(targetPos, out var targetHit, TARGET_SNAP_RADIUS, NavMesh.AllAreas))
                {
                    return null;
                }

                var path = new NavMeshPath();
                if (!NavMesh.CalculatePath(playerHit.position, targetHit.position, NavMesh.AllAreas, path))
                {
                    return false;
                }

                if (path.status == NavMeshPathStatus.PathComplete)
                {
                    return true;
                }

                // Partial path: reachable if it ends close enough to the target for
                // interaction (typical for objects whose NavMesh sample landed on the
                // wrong side of a wall).
                var corners = path.corners;
                if (corners != null && corners.Length > 0)
                {
                    return Vector3.Distance(corners[corners.Length - 1], targetPos) <= CLOSE_ENOUGH;
                }

                return false;
            }
            catch (Exception)
            {
                // Il2Cpp interop hiccup or no NavMesh in this scene - report unknown,
                // never a false "not reachable".
                return null;
            }
        }
    }
}
