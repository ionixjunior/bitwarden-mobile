﻿using System;
using System.Text;
using System.Threading.Tasks;
using Bit.App.Abstractions;
using Bit.App.Models.Api;
using Plugin.Settings.Abstractions;

namespace Bit.App.Services
{
    public class AuthService : IAuthService
    {
        private const string TokenKey = "token";
        private const string EmailKey = "email";
        private const string UserIdKey = "userId";
        private const string PreviousUserIdKey = "previousUserId";
        private const string PinKey = "pin";

        private readonly ISecureStorageService _secureStorage;
        private readonly ISettings _settings;
        private readonly ICryptoService _cryptoService;
        private readonly IAuthApiRepository _authApiRepository;

        private string _token;
        private string _email;
        private string _userId;
        private string _previousUserId;
        private string _pin;

        public AuthService(
            ISecureStorageService secureStorage,
            ISettings settings,
            ICryptoService cryptoService,
            IAuthApiRepository authApiRepository)
        {
            _secureStorage = secureStorage;
            _settings = settings;
            _cryptoService = cryptoService;
            _authApiRepository = authApiRepository;
        }

        public string Token
        {
            get
            {
                if(_token != null)
                {
                    return _token;
                }

                var tokenBytes = _secureStorage.Retrieve(TokenKey);
                if(tokenBytes == null)
                {
                    return null;
                }

                _token = Encoding.UTF8.GetString(tokenBytes, 0, tokenBytes.Length);
                return _token;
            }
            set
            {
                if(value != null)
                {
                    var tokenBytes = Encoding.UTF8.GetBytes(value);
                    _secureStorage.Store(TokenKey, tokenBytes);
                }
                else
                {
                    _secureStorage.Delete(TokenKey);
                }

                _token = value;
            }
        }

        public string UserId
        {
            get
            {
                if(_userId != null)
                {
                    return _userId;
                }

                _userId = _settings.GetValueOrDefault<string>(UserIdKey);
                return _userId;
            }
            set
            {
                if(value != null)
                {
                    _settings.AddOrUpdateValue(UserIdKey, value);
                }
                else
                {
                    PreviousUserId = _userId;
                    _settings.Remove(UserIdKey);
                }

                _userId = value;
            }
        }

        public string PreviousUserId
        {
            get
            {
                if(_previousUserId != null)
                {
                    return _previousUserId;
                }

                _previousUserId = _settings.GetValueOrDefault<string>(PreviousUserIdKey);
                return _previousUserId;
            }
            private set
            {
                if(value != null)
                {
                    _settings.AddOrUpdateValue(PreviousUserIdKey, value);
                    _previousUserId = value;
                }
            }
        }

        public bool UserIdChanged => PreviousUserId != UserId;

        public string Email
        {
            get
            {
                if(_email != null)
                {
                    return _email;
                }

                _email = _settings.GetValueOrDefault<string>(EmailKey);
                return _email;
            }
            set
            {
                if(value != null)
                {
                    _settings.AddOrUpdateValue(EmailKey, value);
                }
                else
                {
                    _settings.Remove(EmailKey);
                }

                _email = value;
            }
        }

        public bool IsAuthenticated
        {
            get
            {
                return _cryptoService.Key != null && Token != null && UserId != null;
            }
        }
        public bool IsAuthenticatedTwoFactor
        {
            get
            {
                return _cryptoService.Key != null && Token != null && UserId == null;
            }
        }

        public string PIN
        {
            get
            {
                if(_pin != null)
                {
                    return _pin;
                }

                var pinBytes = _secureStorage.Retrieve(PinKey);
                if(pinBytes == null)
                {
                    return null;
                }

                _pin = Encoding.UTF8.GetString(pinBytes, 0, pinBytes.Length);
                return _pin;
            }
            set
            {
                if(value != null)
                {
                    var pinBytes = Encoding.UTF8.GetBytes(value);
                    _secureStorage.Store(PinKey, pinBytes);
                }
                else
                {
                    _secureStorage.Delete(PinKey);
                }

                _pin = value;
            }
        }

        public void LogOut()
        {
            Token = null;
            UserId = null;
            Email = null;
            _cryptoService.Key = null;
            _settings.Remove(Constants.FirstVaultLoad);
        }

        public async Task<ApiResult<TokenResponse>> TokenPostAsync(TokenRequest request)
        {
            // TODO: move more logic in here
            return await _authApiRepository.PostTokenAsync(request);
        }

        public async Task<ApiResult<TokenResponse>> TokenTwoFactorPostAsync(TokenTwoFactorRequest request)
        {
            // TODO: move more logic in here
            return await _authApiRepository.PostTokenTwoFactorAsync(request);
        }
    }
}
