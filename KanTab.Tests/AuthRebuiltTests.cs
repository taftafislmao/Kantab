using KanTab.Storage.Supabase;
using KanTab.ViewModels;
using Xunit;

namespace KanTab.Tests;

public class AuthRebuiltTests
{
    private sealed class FakeAuth : IAuthService
    {
        public bool Succeed = true;
        public string FailMsg = "fail";
        public AuthUser? CurrentUser { get; set; }
        public string? GetCurrentAccessToken() => null;
        public System.Threading.Tasks.Task<AuthUser?> RestoreSessionAsync() => System.Threading.Tasks.Task.FromResult<AuthUser?>(null);
        public System.Threading.Tasks.Task<AuthResult> SignInAsync(string e, string p)
        {
            if (Succeed) { CurrentUser = new AuthUser{Id="1",Email=e}; return System.Threading.Tasks.Task.FromResult(AuthResult.Ok(CurrentUser)); }
            return System.Threading.Tasks.Task.FromResult(AuthResult.Fail(FailMsg));
        }
        public System.Threading.Tasks.Task<AuthResult> SignUpAsync(string e, string p)
        {
            if (Succeed) { CurrentUser = new AuthUser{Id="1",Email=e}; return System.Threading.Tasks.Task.FromResult(AuthResult.Ok(CurrentUser)); }
            return System.Threading.Tasks.Task.FromResult(AuthResult.Fail(FailMsg));
        }
        public System.Threading.Tasks.Task SignOutAsync() => System.Threading.Tasks.Task.CompletedTask;
    }

    [Fact] public void NewAuth_Welcome() { var vm=new AuthenticationWindowViewModel(new FakeAuth()); Assert.Equal(AuthMode.Welcome, vm.CurrentMode); Assert.True(vm.IsWelcome); }
    [Fact] public void ShowLogin_Navigates() { var vm=new AuthenticationWindowViewModel(new FakeAuth()); vm.ShowLoginCommand.Execute(null); Assert.Equal(AuthMode.Login, vm.CurrentMode); Assert.True(vm.IsLogin); }
    [Fact] public void ShowSignUp_Navigates() { var vm=new AuthenticationWindowViewModel(new FakeAuth()); vm.ShowSignUpCommand.Execute(null); Assert.Equal(AuthMode.SignUp, vm.CurrentMode); }
    [Fact] public void BackToWelcome_FromLogin() { var vm=new AuthenticationWindowViewModel(new FakeAuth()); vm.ShowLoginCommand.Execute(null); vm.BackToWelcomeCommand.Execute(null); Assert.Equal(AuthMode.Welcome, vm.CurrentMode); }
    [Fact] public void BackToWelcome_FromSignUp() { var vm=new AuthenticationWindowViewModel(new FakeAuth()); vm.ShowSignUpCommand.Execute(null); vm.BackToWelcomeCommand.Execute(null); Assert.True(vm.IsWelcome); }

    [Fact]
    public void Unconfigured_Login_ShowsError_RemainsLogin()
    {
        var fake = new FakeAuth{Succeed=false, FailMsg="Supabase authentication is not configured yet."};
        var vm=new AuthenticationWindowViewModel(fake); vm.ShowLoginCommand.Execute(null);
        vm.Email="a@b.com"; vm.Password="secret12";
        vm.LoginCommand.Execute(null);
        Assert.Equal(AuthMode.Login, vm.CurrentMode);
        Assert.Contains("not configured", vm.ErrorMessage);
    }

    [Fact]
    public void Unconfigured_SignUp_ShowsError_RemainsSignUp()
    {
        var fake = new FakeAuth{Succeed=false, FailMsg="Supabase authentication is not configured yet."};
        var vm=new AuthenticationWindowViewModel(fake); vm.ShowSignUpCommand.Execute(null);
        vm.Email="a@b.com"; vm.Password="secret12"; vm.ConfirmPassword="secret12";
        vm.SignUpCommand.Execute(null);
        Assert.Equal(AuthMode.SignUp, vm.CurrentMode);
        Assert.Contains("not configured", vm.ErrorMessage);
    }

    [Fact]
    public void ContinueOffline_InvokesOnSuccess() { bool ok=false; var vm=new AuthenticationWindowViewModel(new FakeAuth(), ()=>ok=true, canContinueOffline:true); vm.ContinueOfflineCommand.Execute(null); Assert.True(ok); }

    [Fact]
    public void SwitchingScreens_DoesNotInvokeOnSuccess() { bool ok=false; var vm=new AuthenticationWindowViewModel(new FakeAuth(), ()=>ok=true); vm.ShowLoginCommand.Execute(null); vm.BackToWelcomeCommand.Execute(null); Assert.False(ok); }

    [Fact]
    public void FailedAuth_RemainsVisible_DoesNotInvokeOnSuccess()
    {
        bool ok=false; var fake=new FakeAuth{Succeed=false};
        var vm=new AuthenticationWindowViewModel(fake, ()=>ok=true); vm.ShowLoginCommand.Execute(null);
        vm.Email="a@b.com"; vm.Password="secret12"; vm.LoginCommand.Execute(null);
        Assert.False(ok); Assert.Equal(AuthMode.Login, vm.CurrentMode);
    }

    [Fact]
    public void SuccessfulLogin_InvokesOnSuccess_AndRequestsClose()
    {
        bool ok=false; bool closed=false;
        var vm=new AuthenticationWindowViewModel(new FakeAuth{Succeed=true}, ()=>ok=true);
        vm.RequestClose += (_,_)=> closed=true;
        vm.ShowLoginCommand.Execute(null); vm.Email="a@b.com"; vm.Password="secret12"; vm.LoginCommand.Execute(null);
        Assert.True(ok); Assert.True(closed);
    }
}
