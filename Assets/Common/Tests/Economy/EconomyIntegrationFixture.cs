using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.ExceptionServices;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Unity.Services.CloudCode;
using UnityEngine.TestTools;

namespace Unity.Services.Samples.Tests.Economy
{
    /// <summary>
    /// Base for the live economy tests. Each fixture gets its own fresh anonymous player, so
    /// fixtures cannot see each other's wallet, and every test seeds what it needs explicitly.
    /// </summary>
    public abstract class EconomyIntegrationFixture
    {
        protected IPlayerEconomyState State { get; private set; }

        bool m_SignedIn;

        [UnitySetUp]
        public IEnumerator SignInOncePerFixture()
        {
            if (m_SignedIn)
            {
                yield break;
            }

            yield return Await(UgsTestSession.SignInFreshPlayerAsync());
            State = PlayerEconomyState.Create();
            m_SignedIn = true;
        }

        /// <summary>
        /// The Cloud Code SDK logs every failed call as an error, which the test framework
        /// treats as a failure. The flag is per log scope, and each test body gets a fresh one,
        /// so it has to be set from inside the test, right before a call that is meant to fail.
        /// </summary>
        protected static void AllowErrorLogs() => LogAssert.ignoreFailingMessages = true;

        /// <summary>A purchase that may legitimately fail; the SDK logs the refusal as an error.</summary>
        protected Task<PurchaseOutcome> TryPurchase(string purchaseId)
        {
            AllowErrorLogs();
            return State.MakeVirtualPurchaseAsync(purchaseId);
        }

        protected static IEnumerator Await(Task task)
        {
            while (!task.IsCompleted)
            {
                yield return null;
            }

            if (task.IsFaulted)
            {
                ExceptionDispatchInfo.Capture(task.Exception.InnerException ?? task.Exception).Throw();
            }
        }

        protected Task<JObject> Call(string script, Dictionary<string, object> args = null)
            => UgsTestSession.CallScriptAsync(script, args);

        /// <summary>For scripts that return nothing; the SDK would fail to deserialize an empty body.</summary>
        protected Task CallVoid(string script, Dictionary<string, object> args = null)
            => UgsTestSession.CallScriptVoidAsync(script, args);

        protected static Dictionary<string, object> Args(params (string key, object value)[] pairs)
            => UgsTestSession.Args(pairs);

        protected Task<EconomySnapshot> Snapshot() => State.ReadAsync();

        protected static void AssertBalanceDelta(EconomySnapshot before, EconomySnapshot after, string currencyId, long expectedDelta)
        {
            Assert.AreEqual(expectedDelta, after.Balance(currencyId) - before.Balance(currencyId),
                $"{currencyId} delta. before={before} after={after}");
        }

        protected static void AssertItemDelta(EconomySnapshot before, EconomySnapshot after, string itemId, long expectedDelta)
        {
            Assert.AreEqual(expectedDelta, after.Count(itemId) - before.Count(itemId),
                $"{itemId} count delta. before={before} after={after}");
        }

        protected static void AssertUnchanged(EconomySnapshot before, EconomySnapshot after)
        {
            Assert.AreEqual(before.ToString(), after.ToString(), "player state should not have changed");
        }

        /// <summary>
        /// Runs a script call that must fail, and checks the embedded error for a marker that
        /// both worlds produce (a status code or a message fragment from the shared JS).
        /// </summary>
        protected static async Task<CloudCodeException> ExpectScriptError(Func<Task> call, string expectedFragment)
        {
            AllowErrorLogs();
            try
            {
                await call();
            }
            catch (CloudCodeException e)
            {
                StringAssert.Contains(expectedFragment, e.Message);
                return e;
            }

            Assert.Fail($"Expected a Cloud Code error containing '{expectedFragment}', the call succeeded.");
            return null;
        }

        /// <summary>Polls the player state until the predicate holds or the timeout passes.</summary>
        protected async Task<EconomySnapshot> WaitForState(Func<EconomySnapshot, bool> predicate, int timeoutSeconds = 15)
        {
            var deadline = DateTime.UtcNow.AddSeconds(timeoutSeconds);
            EconomySnapshot last;
            do
            {
                last = await Snapshot();
                if (predicate(last))
                {
                    return last;
                }

                await Task.Delay(500);
            }
            while (DateTime.UtcNow < deadline);

            return last;
        }
    }
}
