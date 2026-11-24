using System.Collections;
using Game.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Game.Tests.PlayMode
{
    public class BootstrapPlayModeTests
    {
        [UnityTest]
        public IEnumerator Bootstrap_SetsTargetFrameRate()
        {
            var go = new GameObject("Bootstrap");
            go.AddComponent<Bootstrap>();
            yield return null;
            Assert.AreEqual(60, Application.targetFrameRate);
            Object.Destroy(go);
        }
    }
}
