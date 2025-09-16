using System.CommandLine;
using System.CommandLine.Binding;
using System.IO;

namespace NugetDownloader
{
    public class DownloadOptionsBinder : BinderBase<DownloadOptions>
    {
        private readonly Option<FileInfo> _propsPathOption;
        private readonly Option<DirectoryInfo> _outputDirOption;
        private readonly Option<string[]> _sourcesOption;
        private readonly Option<bool> _disableSslValidationOption;
        private readonly Option<string> _usernameOption;
        private readonly Option<string> _passwordOption;
        private readonly Option<FileInfo> _logFileOption;

        public DownloadOptionsBinder(
            Option<FileInfo> propsPathOption,
            Option<DirectoryInfo> outputDirOption,
            Option<string[]> sourcesOption,
            Option<bool> disableSslValidationOption,
            Option<string> usernameOption,
            Option<string> passwordOption,
            Option<FileInfo> logFileOption)
        {
            _propsPathOption = propsPathOption;
            _outputDirOption = outputDirOption;
            _sourcesOption = sourcesOption;
            _disableSslValidationOption = disableSslValidationOption;
            _usernameOption = usernameOption;
            _passwordOption = passwordOption;
            _logFileOption = logFileOption;
        }

        protected override DownloadOptions GetBoundValue(BindingContext bindingContext)
        {
            return new DownloadOptions
            {
                PropsPath = bindingContext.ParseResult.GetValueForOption(_propsPathOption)!,
                OutputDir = bindingContext.ParseResult.GetValueForOption(_outputDirOption)!,
                NugetSources = bindingContext.ParseResult.GetValueForOption(_sourcesOption)!,
                DisableSslValidation = bindingContext.ParseResult.GetValueForOption(_disableSslValidationOption),
                Username = bindingContext.ParseResult.GetValueForOption(_usernameOption),
                Password = bindingContext.ParseResult.GetValueForOption(_passwordOption),
                LogFile = bindingContext.ParseResult.GetValueForOption(_logFileOption)
            };
        }
    }
}
