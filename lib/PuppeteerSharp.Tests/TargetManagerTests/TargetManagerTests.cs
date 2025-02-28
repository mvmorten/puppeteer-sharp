
using Microsoft.AspNetCore.Http;
using PuppeteerSharp.Tests.Attributes;
using PuppeteerSharp.Nunit;
using System;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using PuppeteerSharp.Helpers;

namespace PuppeteerSharp.Tests.TargetManagerTests
{
    public class TargetManagerTests : PuppeteerBrowserContextBaseTest
    {
        public TargetManagerTests(): base()
        {
            DefaultOptions = TestConstants.DefaultBrowserOptions();
            DefaultOptions.Args = new[]
            {
                "--site-per-process",
                "--remote-debugging-port=21222",
                "--host-rules=\"MAP * 127.0.0.1\"",
            };
        }

        [PuppeteerTest("TargetManager.spec.ts", "TargetManager", "should handle targets")]
        [Skip(SkipAttribute.Targets.Firefox)]

        public async Task ShouldHandleTargets()
        {
            var targetManager = (Browser as Browser).TargetManager;
            Assert.AreEqual(2, targetManager.GetAvailableTargets().Values.Count);

            Assert.IsEmpty(await Context.PagesAsync());
            Assert.AreEqual(2, targetManager.GetAvailableTargets().Values.Count);

            var page = await Context.NewPageAsync();
            Assert.AreEqual(1, (await Context.PagesAsync()).Length);
            Assert.AreEqual(3, targetManager.GetAvailableTargets().Values.Count);

            await page.GoToAsync(TestConstants.EmptyPage);
            Assert.AreEqual(1, (await Context.PagesAsync()).Length);
            Assert.AreEqual(3, targetManager.GetAvailableTargets().Values.Count);

            var frameTask = page.WaitForFrameAsync(target => target.Url == TestConstants.EmptyPage);
            await FrameUtils.AttachFrameAsync(page, "frame1", TestConstants.EmptyPage);
            await frameTask.WithTimeout();
            Assert.AreEqual(1, (await Context.PagesAsync()).Length);
            Assert.AreEqual(3, targetManager.GetAvailableTargets().Values.Count);
            Assert.AreEqual(2, page.Frames.Length);

            frameTask = page.WaitForFrameAsync(target => target.Url == TestConstants.CrossProcessUrl + "/empty.html");
            await FrameUtils.AttachFrameAsync(page, "frame2", TestConstants.CrossProcessUrl + "/empty.html");
            await frameTask.WithTimeout();
            Assert.AreEqual(1, (await Context.PagesAsync()).Length);
            Assert.AreEqual(4, targetManager.GetAvailableTargets().Values.Count);
            Assert.AreEqual(3, page.Frames.Length);

            frameTask = page.WaitForFrameAsync(target => target.Url == TestConstants.CrossProcessUrl + "/empty.html");
            await FrameUtils.AttachFrameAsync(page, "frame3", TestConstants.CrossProcessUrl + "/empty.html");
            await frameTask.WithTimeout();
            Assert.AreEqual(1, (await Context.PagesAsync()).Length);
            Assert.AreEqual(5, targetManager.GetAvailableTargets().Values.Count);
            Assert.AreEqual(4, page.Frames.Length);
        }
    }
}
