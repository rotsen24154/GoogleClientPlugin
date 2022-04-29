using System;
using System.Linq;
using System.Threading.Tasks;
using Android.App;
using Android.Content;
using Android.Gms.Auth.Api.SignIn;
using Android.Gms.Common;
using Android.Gms.Common.Apis;
using Android.Gms.Tasks;
using Java.Interop;
using Plugin.GoogleClient.Shared;
using Object = Java.Lang.Object;
using Task = Android.Gms.Tasks.Task;

namespace Plugin.GoogleClient
{
    /// <summary>
    /// Implementation for GoogleClient
    /// </summary>
    public class GoogleClientManager : Object, IGoogleClientManager, IOnCompleteListener
    {
        // Class Debug Tag
        private static string _tag = typeof(GoogleClientManager).FullName;
        private static int _authActivityId = 9637;
        public static Activity CurrentActivity { get; set; }
        static System.Threading.Tasks.TaskCompletionSource<GoogleResponse<GoogleUser>> _loginTcs;
        static string _serverClientId;
        static string _clientId;
        static string[] _initScopes = Array.Empty<string>();

        private GoogleSignInClient _mGoogleSignInClient;

        public GoogleUser CurrentUser
        {
            get
            {
                GoogleSignInAccount userAccount = GoogleSignIn.GetLastSignedInAccount(CurrentActivity);
                return userAccount != null ? new GoogleUser
                {
                    Id = userAccount.Id,
                    Name = userAccount.DisplayName,
                    GivenName = userAccount.GivenName,
                    FamilyName = userAccount.FamilyName,
                    Email = userAccount.Email,
                    Picture = new Uri((userAccount.PhotoUrl != null ? $"{userAccount.PhotoUrl}" : "https://autisticdating.net/imgs/profile-placeholder.jpg"))
                } : null;
            }
        }

        public static readonly string[] DefaultScopes = {
            Scopes.Profile
        };


        internal GoogleClientManager()
        {
            if (CurrentActivity == null)
            {
                throw new GoogleClientNotInitializedErrorException(GoogleClientBaseException.ClientNotInitializedErrorMessage);
            }

            var gopBuilder = new GoogleSignInOptions.Builder(GoogleSignInOptions.DefaultSignIn)
                    .RequestEmail();

            if (!string.IsNullOrWhiteSpace(_serverClientId))
            {
                gopBuilder.RequestServerAuthCode(_serverClientId, false);
            }

            if (!string.IsNullOrWhiteSpace(_clientId))
            {
                gopBuilder.RequestIdToken(_clientId);
            }

            foreach (var s in _initScopes)
            {
                gopBuilder.RequestScopes(new Scope(s));
            }

            var googleSignInOptions = gopBuilder.Build();

            // Build a GoogleSignInClient with the options specified by gso.
            _mGoogleSignInClient = GoogleSignIn.GetClient(CurrentActivity, googleSignInOptions);
        }

        public static void Initialize(
            Activity activity,
            string serverClientId = null,
            string clientId = null,
            string[] scopes = null,
            int requestCode = 9637)
        {
            CurrentActivity = activity;
            _serverClientId = serverClientId;
            _clientId = clientId;
            _initScopes = DefaultScopes.Concat(scopes ?? Array.Empty<string>()).ToArray();
            _authActivityId = requestCode;
        }

        EventHandler<GoogleClientResultEventArgs<GoogleUser>> _onLogin;
        public event EventHandler<GoogleClientResultEventArgs<GoogleUser>> OnLogin
        {
            add => _onLogin += value;
            remove => _onLogin -= value;
        }

        public async Task<GoogleResponse<GoogleUser>> LoginAsync()
        {
            if (CurrentActivity == null || _mGoogleSignInClient == null)
            {
                throw new GoogleClientNotInitializedErrorException(GoogleClientBaseException.ClientNotInitializedErrorMessage);
            }

            _loginTcs = new TaskCompletionSource<GoogleResponse<GoogleUser>>();

            GoogleSignInAccount account = GoogleSignIn.GetLastSignedInAccount(CurrentActivity);


            if (account != null)
            {
                OnSignInSuccessful(account);
            }
            else
            {
                Intent intent = _mGoogleSignInClient.SignInIntent;
                CurrentActivity?.StartActivityForResult(intent, _authActivityId);
            }

            return await _loginTcs.Task;
        }

        public async Task<GoogleResponse<GoogleUser>> SilentLoginAsync()
        {

            if (CurrentActivity == null || _mGoogleSignInClient == null)
            {
                throw new GoogleClientNotInitializedErrorException(GoogleClientBaseException.ClientNotInitializedErrorMessage);
            }

            _loginTcs = new TaskCompletionSource<GoogleResponse<GoogleUser>>();

            var account = GoogleSignIn.GetLastSignedInAccount(CurrentActivity);

            if (account != null)
            {
                OnSignInSuccessful(account);
            }
            else
            {
                var userAccount = await _mGoogleSignInClient.SilentSignInAsync();
                OnSignInSuccessful(userAccount);
            }

            return await _loginTcs.Task;
        }

        EventHandler _onLogout;
        public event EventHandler OnLogout
        {
            add => _onLogout += value;
            remove => _onLogout -= value;
        }

        protected virtual void OnLogoutCompleted(EventArgs e) => _onLogout?.Invoke(this, e);

        public void Logout()
        {
            if (CurrentActivity == null || _mGoogleSignInClient == null)
            {
                throw new GoogleClientNotInitializedErrorException(GoogleClientBaseException.ClientNotInitializedErrorMessage);
            }

            if (GoogleSignIn.GetLastSignedInAccount(CurrentActivity) == null) return;
            _idToken = null;
            _accessToken = null;
            _mGoogleSignInClient.SignOut();
            OnLogoutCompleted(EventArgs.Empty);
        }

        public bool IsLoggedIn
        {
            get
            {
                if (CurrentActivity == null)
                {
                    throw new GoogleClientNotInitializedErrorException(GoogleClientBaseException.ClientNotInitializedErrorMessage);
                }
                return GoogleSignIn.GetLastSignedInAccount(CurrentActivity) != null;
            }
        }

        public string IdToken { get { return _idToken; } }
        public string AccessToken { get { return _accessToken; } }
        static string _idToken { get; set; }
        static string _accessToken { get; set; }

        EventHandler<GoogleClientErrorEventArgs> _onError;
        public event EventHandler<GoogleClientErrorEventArgs> OnError
        {
            add => _onError += value;
            remove => _onError -= value;
        }

        protected virtual void OnGoogleClientError(GoogleClientErrorEventArgs e) => _onError?.Invoke(this, e);

        public static void OnAuthCompleted(int requestCode, Result resultCode, Intent data)
        {
            if (requestCode != _authActivityId)
            {
                return;
            }
            GoogleSignIn.GetSignedInAccountFromIntent(data).AddOnCompleteListener(CrossGoogleClient.Current as IOnCompleteListener);

        }

        void OnSignInSuccessful(GoogleSignInAccount userAccount)
        {
            try
            {
                GoogleUser googleUser = new GoogleUser
                {
                    Id = userAccount.Id,
                    Name = userAccount.DisplayName,
                    GivenName = userAccount.GivenName,
                    FamilyName = userAccount.FamilyName,
                    Email = userAccount.Email,
                    Picture = new Uri((userAccount.PhotoUrl != null
                        ? $"{userAccount.PhotoUrl}"
                        : "https://autisticdating.net/imgs/profile-placeholder.jpg"))
                };

                _idToken = userAccount.IdToken;
                var googleArgs = new GoogleClientResultEventArgs<GoogleUser>(googleUser, GoogleActionStatus.Completed);

                if (_onLogin == null || _loginTcs == null || googleArgs.Data == null) return;
                // Send the result to the receivers
                _onLogin?.Invoke(CrossGoogleClient.Current, googleArgs);
                _loginTcs.TrySetResult(new GoogleResponse<GoogleUser>(googleArgs));
            }
            catch (Exception e)
            {

            }
        }

        void OnSignInFailed(ApiException apiException)
        {
            GoogleClientErrorEventArgs errorEventArgs = new GoogleClientErrorEventArgs();
            Exception exception = null;

            switch (apiException.StatusCode)
            {
                case GoogleSignInStatusCodes.InternalError:
                    errorEventArgs.Error = GoogleClientErrorType.SignInInternalError;
                    errorEventArgs.Message = GoogleClientBaseException.SignInInternalErrorMessage;
                    exception = new GoogleClientSignInInternalErrorException();
                    break;
                case GoogleSignInStatusCodes.ApiNotConnected:
                    errorEventArgs.Error = GoogleClientErrorType.SignInApiNotConnectedError;
                    errorEventArgs.Message = GoogleClientBaseException.SignInApiNotConnectedErrorMessage;
                    exception = new GoogleClientSignInApiNotConnectedErrorException();
                    break;
                case GoogleSignInStatusCodes.NetworkError:
                    errorEventArgs.Error = GoogleClientErrorType.SignInNetworkError;
                    errorEventArgs.Message = GoogleClientBaseException.SignInNetworkErrorMessage;
                    exception = new GoogleClientSignInNetworkErrorException();
                    break;
                case GoogleSignInStatusCodes.InvalidAccount:
                    errorEventArgs.Error = GoogleClientErrorType.SignInInvalidAccountError;
                    errorEventArgs.Message = GoogleClientBaseException.SignInInvalidAccountErrorMessage;
                    exception = new GoogleClientSignInInvalidAccountErrorException();
                    break;
                case GoogleSignInStatusCodes.SignInRequired:
                    errorEventArgs.Error = GoogleClientErrorType.SignInRequiredError;
                    errorEventArgs.Message = GoogleClientBaseException.SignInRequiredErrorMessage;
                    exception = new GoogleClientSignInRequiredErrorErrorException();
                    break;
                case GoogleSignInStatusCodes.SignInFailed:
                    errorEventArgs.Error = GoogleClientErrorType.SignInFailedError;
                    errorEventArgs.Message = GoogleClientBaseException.SignInFailedErrorMessage;
                    exception = new GoogleClientSignInFailedErrorException();
                    break;
                case GoogleSignInStatusCodes.SignInCancelled:
                    errorEventArgs.Error = GoogleClientErrorType.SignInCanceledError;
                    errorEventArgs.Message = GoogleClientBaseException.SignInCanceledErrorMessage;
                    exception = new GoogleClientSignInCanceledErrorException();
                    break;
                default:
                    errorEventArgs.Error = GoogleClientErrorType.SignInDefaultError;
                    errorEventArgs.Message = apiException.Message;
                    exception = new GoogleClientBaseException(
                        string.IsNullOrEmpty(apiException.Message)
                            ? GoogleClientBaseException.SignInDefaultErrorMessage
                            : apiException.Message
                        );
                    break;
            }

            _onError?.Invoke(CrossGoogleClient.Current, errorEventArgs);
            _loginTcs.TrySetException(exception);
        }

        public void OnComplete(Task task)
        {
            if (!task.IsSuccessful)
            {
                //Failed
                OnSignInFailed(task.Exception.JavaCast<ApiException>());
            }
            else
            {
                var userAccount = task.Result.JavaCast<GoogleSignInAccount>();

                OnSignInSuccessful(userAccount);

            }


        }
    }
}
