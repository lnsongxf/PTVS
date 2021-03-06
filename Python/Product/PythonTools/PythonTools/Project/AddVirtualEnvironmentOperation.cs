// Python Tools for Visual Studio
// Copyright(c) Microsoft Corporation
// All rights reserved.
//
// Licensed under the Apache License, Version 2.0 (the License); you may not use
// this file except in compliance with the License. You may obtain a copy of the
// License at http://www.apache.org/licenses/LICENSE-2.0
//
// THIS CODE IS PROVIDED ON AN  *AS IS* BASIS, WITHOUT WARRANTIES OR CONDITIONS
// OF ANY KIND, EITHER EXPRESS OR IMPLIED, INCLUDING WITHOUT LIMITATION ANY
// IMPLIED WARRANTIES OR CONDITIONS OF TITLE, FITNESS FOR A PARTICULAR PURPOSE,
// MERCHANTABLITY OR NON-INFRINGEMENT.
//
// See the Apache Version 2.0 License for specific language governing
// permissions and limitations under the License.

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.PythonTools.Infrastructure;
using Microsoft.PythonTools.Interpreter;

namespace Microsoft.PythonTools.Project {
    sealed class AddVirtualEnvironmentOperation : IPackageManagerUI {
        private readonly PythonProjectNode _project;
        private readonly string _virtualEnvPath;
        private readonly IPythonInterpreterFactory _baseInterpreter;
        private readonly bool _create;
        private readonly bool _useVEnv;
        private readonly bool _installRequirements;
        private readonly Redirector _output;
        
        public AddVirtualEnvironmentOperation(
            PythonProjectNode project,
            string virtualEnvPath,
            IPythonInterpreterFactory baseInterpreter,
            bool create,
            bool useVEnv,
            bool installRequirements,
            Redirector output = null
        ) {
            _project = project;
            _virtualEnvPath = virtualEnvPath;
            _baseInterpreter = baseInterpreter;
            _create = create;
            _useVEnv = useVEnv;
            _installRequirements = installRequirements;
            _output = output;
        }

        private void WriteOutput(string message) {
            if (_output != null) {
                _output.WriteLine(message);
            }
        }

        private void WriteError(string message) {
            if (_output != null) {
                _output.WriteErrorLine(message);
            }
        }
        
        public async Task Run() {
            var service = _project.Site.GetComponentModel().GetService<IInterpreterRegistryService>();

            IPythonInterpreterFactory factory;
            try {
                factory = await _project.CreateOrAddVirtualEnvironment(
                    service,
                    _create,
                    _virtualEnvPath,
                    _baseInterpreter,
                    _useVEnv
                );
            } catch (Exception ex) when (!ex.IsCriticalException()) {
                WriteError(ex.Message);
                factory = null;
            }

            if (factory == null) {
                return;
            }

            var txt = PathUtils.GetAbsoluteFilePath(_project.ProjectHome, "requirements.txt");
            if (!_installRequirements || !File.Exists(txt)) {
                return;
            }

            if (factory.PackageManager == null) {
                WriteError(
                    Strings.PackageManagementNotSupported_Package.FormatUI(PathUtils.GetFileOrDirectoryName(txt))
                );
                return;
            }

            WriteOutput(Strings.RequirementsTxtInstalling.FormatUI(txt));
            if (await factory.PackageManager.InstallAsync(
                PackageSpec.FromArguments("-r " + ProcessOutput.QuoteSingleArgument(txt)),
                this,
                CancellationToken.None
            )) {
                WriteOutput(Strings.PackageInstallSucceeded.FormatUI(Path.GetFileName(txt)));
            } else {
                WriteOutput(Strings.PackageInstallFailed.FormatUI(Path.GetFileName(txt)));
            }
        }

        void IPackageManagerUI.OnOutputTextReceived(string text) {
            _output.WriteLine(text.TrimEndNewline());
        }

        void IPackageManagerUI.OnErrorTextReceived(string text) {
            _output.WriteErrorLine(text.TrimEndNewline());
        }

        void IPackageManagerUI.OnOperationStarted(string operation) { }

        void IPackageManagerUI.OnOperationFinished(string operation, bool success) { }

        Task<bool> IPackageManagerUI.ShouldElevateAsync(string operation) {
            return Task.FromResult(_project.Site.GetPythonToolsService().GeneralOptions.ElevatePip);
        }
    }
}
