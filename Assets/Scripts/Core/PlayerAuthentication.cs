using System.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.Core;
using UnityEngine;

namespace CrittersOfCalamity.Core
{
    public static class PlayerAuthentication
    {
        public static Task<bool> SignedIn { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Initialize()
        {
            SignedIn = SignInAsync();
        }

        static async Task<bool> SignInAsync()
        {
            try
            {
                if (UnityServices.State == ServicesInitializationState.Uninitialized)
                {
                    await UnityServices.InitializeAsync();
                }

                if (!AuthenticationService.Instance.IsSignedIn)
                {
                    await AuthenticationService.Instance.SignInAnonymouslyAsync();
                }

                Debug.Log($"[PlayerAuthentication] Signed in as PlayerId: {AuthenticationService.Instance.PlayerId}");
                return true;
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[PlayerAuthentication] Anonymous sign-in skipped or failed: {ex.Message}");
                return false;
            }
        }
    }
}
