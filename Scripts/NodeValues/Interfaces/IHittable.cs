using Cysharp.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Threading;

namespace MadApper.GridSystem
{
    public interface IHittableViaNeighbour : INodeValue
    {
        public const string k_Tag = "Hittable Neighbour";
        public void i_HitViaNeighbour(BoardEntity hitter, Action onCompleted, CancellationToken cToken);

    }
    public interface IHittableViaSameNode : INodeValue
    {
        public const string k_Tag = "Hittable Node";

        public void i_HitViaSameNode(BoardEntity hitter, Action onCompleted, CancellationToken cToken);
    }





    public static class IHittableExtentions
    {
        public static async UniTask TryHitNeighboursHittables(this HashSet<BoardEntity> hitters,
           Func<BoardEntity, IEnumerable<BoardEntity>> getNeighbours,
            CancellationToken cToken)
        {
            var dict = new Dictionary<IHittableViaNeighbour, BoardEntity>(hitters.Count);

            foreach (var hitter in hitters)
            {
                foreach (var neighbour in getNeighbours(hitter))
                {
                    var value = neighbour.NodeValue;

                    if (value is IHittableViaNeighbour iHittable)
                    {
                        // for those items like Pilaster that have different subobjects
                        // and they only have to be hitted once!                      

                        if (dict.ContainsKey(iHittable)) continue;
                        dict.Add(iHittable, hitter);
                    }
                }
            }

            var tasks = dict.Count;
            foreach (var item in dict) item.Key.i_HitViaNeighbour(item.Value, onCompleted: () => tasks--, cToken: cToken);
            await UniTask.WaitUntil(() => tasks <= 0, cancellationToken: cToken);
        }

        public static async UniTask TryHitSameNodeHittables(this HashSet<BoardEntity> hitters,
            Func<BoardEntity, IEnumerable<BoardEntity>> getSameNodeValues,
            CancellationToken cToken)
        {
            var dict = new Dictionary<IHittableViaSameNode, BoardEntity>(hitters.Count);

            foreach (var hitter in hitters)
            {
                foreach (var sameNodeVal in getSameNodeValues(hitter))
                {
                    var value = sameNodeVal.NodeValue;

                    if (value is IHittableViaSameNode iHittable)
                    {
                        // for those items like Pilaster that have different subobjects
                        // and they only have to be hitted once!                      

                        if (dict.ContainsKey(iHittable)) continue;
                        dict.Add(iHittable, hitter);
                    }
                }
            }

            var tasks = dict.Count;
            foreach (var item in dict) item.Key.i_HitViaSameNode(item.Value, onCompleted: () => tasks--, cToken: cToken);
            await UniTask.WaitUntil(() => tasks <= 0, cancellationToken: cToken);
        }

    }
}
