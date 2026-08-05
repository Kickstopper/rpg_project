using System.Collections.Generic;
using UnityEngine;

public static class YieldCache
{
    private static readonly Dictionary<float, WaitForSeconds> _waitForSeconds = new Dictionary<float, WaitForSeconds>(new FloatComparer());

    public static WaitForSeconds WaitForSeconds(float seconds)
    {
        if (!_waitForSeconds.TryGetValue(seconds, out var wfs))
        {
            _waitForSeconds.Add(seconds, wfs = new WaitForSeconds(seconds));
        }
        return wfs;
    }

    // float 비교 시 박싱 가비지 방지를 위한 커스텀 비교자
    class FloatComparer : IEqualityComparer<float>
    {
        public bool Equals(float x, float y) => x == y;
        public int GetHashCode(float obj) => obj.GetHashCode();
    }
}