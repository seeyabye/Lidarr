using System;
using System.Collections.Generic;
using System.IO;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Common;
using NzbDrone.Common.Disk;
using NzbDrone.Common.EnvironmentInfo;
using NzbDrone.Common.Extensions;
using NzbDrone.Common.Http;
using NzbDrone.Common.Model;
using NzbDrone.Common.Processes;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.Messaging.Commands;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Core.Update;
using NzbDrone.Core.Update.Commands;
using NzbDrone.Test.Common;
using NzbDrone.Test.Common.Categories;

namespace NzbDrone.Core.Test.UpdateTests
{
    [TestFixture]
    public class UpdateServiceFixture : CoreTest<InstallUpdateService>
    {
        private string _sandboxFolder;

        private UpdatePackage _updatePackage;

        [SetUp]
        public void Setup()
        {
            if (OsInfo.IsLinux)
            {
                _updatePackage = new UpdatePackage
                {
                    FileName = "Lidarr.develop.0.6.2.883.tar.gz",
                    Url = "https://github.com/lidarr/Lidarr/releases/download/v0.6.2.883/Lidarr.develop.0.6.2.883.linux.tar.gz",
                    Version = new Version("0.6.2.883")
                };
            }

            else
            {
                _updatePackage = new UpdatePackage
                {
                    FileName = "Lidarr.develop.0.6.2.883.zip",
                    Url = "https://github.com/lidarr/Lidarr/releases/download/v0.6.2.883/Lidarr.develop.0.6.2.883.windows.zip",
                    Version = new Version("0.6.2.883")
                };
            }

            Mocker.GetMock<IAppFolderInfo>().SetupGet(c => c.TempFolder).Returns(TempFolder);
            Mocker.GetMock<IAppFolderInfo>().SetupGet(c => c.StartUpFolder).Returns(@"C:\Lidarr".AsOsAgnostic);
            Mocker.GetMock<IAppFolderInfo>().SetupGet(c => c.AppDataFolder).Returns(@"C:\ProgramData\Lidarr".AsOsAgnostic);

            Mocker.GetMock<ICheckUpdateService>().Setup(c => c.AvailableUpdate()).Returns(_updatePackage);
            Mocker.GetMock<IVerifyUpdates>().Setup(c => c.Verify(It.IsAny<UpdatePackage>(), It.IsAny<string>())).Returns(true);

            Mocker.GetMock<IProcessProvider>().Setup(c => c.GetCurrentProcess()).Returns(new ProcessInfo { Id = 12 });
            Mocker.GetMock<IRuntimeInfo>().Setup(c => c.ExecutingApplication).Returns(@"C:\Test\Lidarr.exe");

            Mocker.GetMock<IConfigFileProvider>()
                  .SetupGet(s => s.UpdateAutomatically)
                  .Returns(true);

            Mocker.GetMock<IDiskProvider>()
                  .Setup(c => c.FolderWritable(It.IsAny<string>()))
                  .Returns(true);

            _sandboxFolder = Mocker.GetMock<IAppFolderInfo>().Object.GetUpdateSandboxFolder();
        }

        private void GivenInstallScript(string path)
        {
            Mocker.GetMock<IConfigFileProvider>()
                  .SetupGet(s => s.UpdateMechanism)
                  .Returns(UpdateMechanism.Script);

            Mocker.GetMock<IConfigFileProvider>()
                  .SetupGet(s => s.UpdateScriptPath)
                  .Returns(path);

            Mocker.GetMock<IDiskProvider>()
                  .Setup(s => s.FileExists(path, StringComparison.Ordinal))
                  .Returns(true);
        }

        [Test]
        public void should_not_update_if_inside_docker()
        {
            Mocker.GetMock<IOsInfo>().Setup(x => x.IsDocker).Returns(true);

            Subject.Execute(new ApplicationUpdateCommand());

            Mocker.GetMock<IProcessProvider>()
                .Verify(c => c.Start(It.IsAny<string>(), It.Is<string>(s => s.StartsWith("12")), null, null, null), Times.Never());
        }

        [Test]
        public void should_delete_sandbox_before_update_if_folder_exists()
        {
            Mocker.GetMock<IDiskProvider>().Setup(c => c.FolderExists(_sandboxFolder)).Returns(true);

            Subject.Execute(new ApplicationUpdateCommand());

            Mocker.GetMock<IDiskProvider>().Verify(c => c.DeleteFolder(_sandboxFolder, true));
        }

        [Test]
        public void should_not_delete_sandbox_before_update_if_folder_doesnt_exists()
        {
            Mocker.GetMock<IDiskProvider>().Setup(c => c.FolderExists(_sandboxFolder)).Returns(false);

            Subject.Execute(new ApplicationUpdateCommand());


            Mocker.GetMock<IDiskProvider>().Verify(c => c.DeleteFolder(_sandboxFolder, true), Times.Never());
        }

        [Test]
        public void Should_download_update_package()
        {
            var updateArchive = Path.Combine(_sandboxFolder, _updatePackage.FileName);

            Subject.Execute(new ApplicationUpdateCommand());

            Mocker.GetMock<IHttpClient>().Verify(c => c.DownloadFile(_updatePackage.Url, updateArchive));
        }

        [Test]
        public void Should_extract_update_package()
        {
            var updateArchive = Path.Combine(_sandboxFolder, _updatePackage.FileName);

            Subject.Execute(new ApplicationUpdateCommand());

            Mocker.GetMock<IArchiveService>().Verify(c => c.Extract(updateArchive, _sandboxFolder));
        }

        [Test]
        public void Should_copy_update_client_to_root_of_sandbox()
        {
            var updateClientFolder = Mocker.GetMock<IAppFolderInfo>().Object.GetUpdateClientFolder();

            Subject.Execute(new ApplicationUpdateCommand());

            Mocker.GetMock<IDiskTransferService>()
                  .Verify(c => c.TransferFolder(updateClientFolder, _sandboxFolder, TransferMode.Move, false));
        }

        [Test]
        public void should_start_update_client()
        {
            Subject.Execute(new ApplicationUpdateCommand());

            Mocker.GetMock<IProcessProvider>()
                .Verify(c => c.Start(It.IsAny<string>(), It.Is<string>(s => s.StartsWith("12")), null, null, null), Times.Once());
        }

        [Test]
        public void should_return_without_error_or_warnings_when_no_updates_are_available()
        {
            Mocker.GetMock<ICheckUpdateService>().Setup(c => c.AvailableUpdate()).Returns<UpdatePackage>(null);

            Subject.Execute(new ApplicationUpdateCommand());


            ExceptionVerification.AssertNoUnexpectedLogs();
        }

        [Test]
        public void should_not_extract_if_verification_fails()
        {
            Mocker.GetMock<IVerifyUpdates>().Setup(c => c.Verify(It.IsAny<UpdatePackage>(), It.IsAny<string>())).Returns(false);

            Assert.Throws<CommandFailedException>(() => Subject.Execute(new ApplicationUpdateCommand()));

            Mocker.GetMock<IArchiveService>().Verify(v => v.Extract(It.IsAny<string>(), It.IsAny<string>()), Times.Never());
        }

        [Test]
        [Platform("Mono")]
        public void should_run_script_if_configured()
        {
            const string scriptPath = "/tmp/lidarr/update.sh";

            GivenInstallScript(scriptPath);

            Subject.Execute(new ApplicationUpdateCommand());

            Mocker.GetMock<IProcessProvider>().Verify(v => v.Start(scriptPath, It.IsAny<string>(), null, null, null), Times.Once());
        }

        [Test]
        [Platform("Mono")]
        public void should_throw_if_script_is_not_set()
        {
            const string scriptPath = "/tmp/lidarr/update.sh";

            GivenInstallScript("");

            Assert.Throws<CommandFailedException>(() => Subject.Execute(new ApplicationUpdateCommand()));

            ExceptionVerification.ExpectedErrors(1);
            Mocker.GetMock<IProcessProvider>().Verify(v => v.Start(scriptPath, It.IsAny<string>(), null, null, null), Times.Never());
        }

        [Test]
        [Platform("Mono")]
        public void should_throw_if_script_is_null()
        {
            const string scriptPath = "/tmp/lidarr/update.sh";

            GivenInstallScript(null);

            Assert.Throws<CommandFailedException>(() => Subject.Execute(new ApplicationUpdateCommand()));

            ExceptionVerification.ExpectedErrors(1);
            Mocker.GetMock<IProcessProvider>().Verify(v => v.Start(scriptPath, It.IsAny<string>(), null, null, null), Times.Never());
        }

        [Test]
        [Platform("Mono")]
        public void should_throw_if_script_path_does_not_exist()
        {
            const string scriptPath = "/tmp/lidarr/update.sh";

            GivenInstallScript(scriptPath);

            Mocker.GetMock<IDiskProvider>()
                  .Setup(s => s.FileExists(scriptPath, StringComparison.Ordinal))
                  .Returns(false);

            Assert.Throws<CommandFailedException>(() => Subject.Execute(new ApplicationUpdateCommand()));

            ExceptionVerification.ExpectedErrors(1);
            Mocker.GetMock<IProcessProvider>().Verify(v => v.Start(scriptPath, It.IsAny<string>(), null, null, null), Times.Never());
        }

        [Test]
        [IntegrationTest]
        public void Should_download_and_extract_to_temp_folder()
        {
            UseRealHttp();

            var updateSubFolder = new DirectoryInfo(Mocker.GetMock<IAppFolderInfo>().Object.GetUpdateSandboxFolder());

            updateSubFolder.Exists.Should().BeFalse();

            Mocker.SetConstant<IArchiveService>(Mocker.Resolve<ArchiveService>());

            Subject.Execute(new ApplicationUpdateCommand());

            updateSubFolder.Refresh();

            updateSubFolder.Exists.Should().BeTrue();
            updateSubFolder.GetDirectories("Lidarr").Should().HaveCount(1);
            updateSubFolder.GetDirectories().Should().HaveCount(1);
            updateSubFolder.GetFiles().Should().NotBeEmpty();
        }

        [Test]
        public void should_log_error_when_app_data_is_child_of_startup_folder()
        {
            Mocker.GetMock<IAppFolderInfo>().SetupGet(c => c.StartUpFolder).Returns(@"C:\Lidarr".AsOsAgnostic);
            Mocker.GetMock<IAppFolderInfo>().SetupGet(c => c.AppDataFolder).Returns(@"C:\Lidarr\AppData".AsOsAgnostic);

            Assert.Throws<CommandFailedException>(() => Subject.Execute(new ApplicationUpdateCommand()));
            ExceptionVerification.ExpectedErrors(1);
        }

        [Test]
        public void should_log_error_when_app_data_is_same_as_startup_folder()
        {
            Mocker.GetMock<IAppFolderInfo>().SetupGet(c => c.StartUpFolder).Returns(@"C:\NzbDrone".AsOsAgnostic);
            Mocker.GetMock<IAppFolderInfo>().SetupGet(c => c.AppDataFolder).Returns(@"C:\NzbDrone".AsOsAgnostic);

            Assert.Throws<CommandFailedException>(() => Subject.Execute(new ApplicationUpdateCommand()));
            ExceptionVerification.ExpectedErrors(1);
        }

        [Test]
        public void should_log_error_when_startup_folder_is_not_writable()
        {
            Mocker.GetMock<IDiskProvider>()
                  .Setup(c => c.FolderWritable(It.IsAny<string>()))
                  .Returns(false);

            var updateArchive = Path.Combine(_sandboxFolder, _updatePackage.FileName);

            Assert.Throws<CommandFailedException>(() => Subject.Execute(new ApplicationUpdateCommand()));

            Mocker.GetMock<IHttpClient>().Verify(c => c.DownloadFile(_updatePackage.Url, updateArchive), Times.Never());
            ExceptionVerification.ExpectedErrors(1);
        }

        [Test]
        public void should_log_when_install_cannot_be_started()
        {
            Mocker.GetMock<IDiskProvider>()
                  .Setup(c => c.FolderWritable(It.IsAny<string>()))
                  .Returns(false);

            var updateArchive = Path.Combine(_sandboxFolder, _updatePackage.FileName);

            Assert.Throws<CommandFailedException>(() => Subject.Execute(new ApplicationUpdateCommand()));

            Mocker.GetMock<IHttpClient>().Verify(c => c.DownloadFile(_updatePackage.Url, updateArchive), Times.Never());
            ExceptionVerification.ExpectedErrors(1);
        }

        [Test]
        public void should_switch_to_branch_specified_in_updatepackage()
        {
            _updatePackage.Branch = "fake";

            Subject.Execute(new ApplicationUpdateCommand());

            Mocker.GetMock<IConfigFileProvider>()
                  .Verify(v => v.SaveConfigDictionary(It.Is<Dictionary<string, object>>(d => d.ContainsKey("Branch") && (string)d["Branch"] == "fake")), Times.Once());
        }

        [TearDown]
        public void TearDown()
        {
            ExceptionVerification.IgnoreErrors();
        }
    }
}
