using System.Net;
using System.Text;
using Pulse.Core;
using Pulse.Infrastructure;
using Xunit;

namespace Pulse.Tests;

public sealed class ProfileModeTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-10-22T10:00:00Z");
    [Theory] [InlineData(Profile.Gaming,"Gaming")] [InlineData(Profile.Work,"Developer")] [InlineData(Profile.Stream,"Creator")] [InlineData(Profile.Focus,"Focus")] [InlineData(Profile.Default,"Default")]
    public void BuiltInProfilesHaveFunctionalModes(Profile profile,string expected) => Assert.Equal(expected,ModePresets.For(ProfileWorkspaces.Default(profile)));
    [Fact] public void PresetsReduceDistractionsWithoutReplacingAppsAndCustomProfilesInheritTheMode()
    {
        var original=ProfileWorkspaces.Default(Profile.Work); var focus=ModePresets.Apply(original,"Focus");
        Assert.Equal(original.Apps,focus.Apps); Assert.Equal(["Timer"],focus.Widgets);
        var custom=ProfileCustomization.Create(new PulseSettings(),"My studio",Profile.Stream);
        Assert.Equal("Creator",ModePresets.For(custom.Workspaces.Last()));
    }
    [Theory] [InlineData("https://github.com/me/repo/","me/repo")] [InlineData("me/repo","me/repo")] [InlineData("me/repo/actions",null)] [InlineData("https://evil.test/me/repo",null)] [InlineData("../repo",null)] [InlineData("me/repo?token=x",null)]
    public void PinnedRepositoryNamesAreConstrained(string input,string? expected) => Assert.Equal(expected,RepositoryName.Normalize(input));
    [Fact] public void GameTimePausesAcrossAltTabAndResetsForAnotherGame()
    {
        var clock=new GameSessionClock(); var app=new ForegroundApplication(1,"game","game.exe");
        clock.Observe(app,true,Now);clock.Observe(app,true,Now.AddSeconds(3));clock.Observe(app,false,Now.AddSeconds(4));
        clock.Observe(app,true,Now.AddMinutes(1));Assert.Equal(TimeSpan.FromSeconds(3),clock.Elapsed);
        clock.Observe(app with {ProcessId=2},true,Now.AddMinutes(2));Assert.Equal(TimeSpan.Zero,clock.Elapsed);
    }
    [Fact] public void CreatorPeakSamplingSleepsWhenTheMeterIsHidden()
    {Assert.Equal(100,new RuntimeDemand {MicrophonePeak=true}.AudioIntervalMs);Assert.Equal(2000,new RuntimeDemand().AudioIntervalMs);Assert.False(new RuntimeDemand {MicrophonePeak=true}.AudioPeak);}
    [Fact] public void FpsDefaultsToGamingAndOtherModesCanOptInWithoutRenamingProfiles()
    { Assert.True(ProfileWorkspaces.Default(Profile.Gaming).WantsFps());Assert.False(ProfileWorkspaces.Default(Profile.Work).WantsFps());Assert.True((ProfileWorkspaces.Default(Profile.Work) with {ShowFps=true}).WantsFps()); }
    private static string Calendar(string entries) => "BEGIN:VCALENDAR\r\nVERSION:2.0\r\n"+entries+"END:VCALENDAR\r\n";
    private static string Event(string extra="",string start="20261022T110000Z",string end="20261022T120000Z") => $"BEGIN:VEVENT\r\nUID:one\r\nDTSTART:{start}\r\nDTEND:{end}\r\nSUMMARY:Team\\, sync\r\n{extra}END:VEVENT\r\n";
    [Fact] public void CalendarUnfoldsNamesHandlesUtcAndDoesNotLaunchUnsafeMeetingLinks()
    {
        var value=CalendarMeetings.Parse(Calendar(Event("URL:javascript:alert(1)\r\n")),Now);
        Assert.Equal("Team, sync",value.Next?.Title); Assert.Null(value.Next?.JoinUrl);Assert.Equal(Now.AddHours(1),value.Next?.Start);
        var folded=CalendarMeetings.Parse(Calendar(Event().Replace("SUMMARY:Team\\, sync","SUMMARY:Team\\,\r\n  sync")),Now);Assert.Equal("Team, sync",folded.Next?.Title);
    }
    [Fact] public void CalendarCancelsAllDayAndExpiredEventsAndReportsUnsupportedRules()
    {
        Assert.Null(CalendarMeetings.Parse(Calendar(Event("STATUS:CANCELLED\r\n")),Now).Next);
        Assert.Null(CalendarMeetings.Parse(Calendar(Event(start:"20261022",end:"20261023")),Now).Next);
        Assert.Null(CalendarMeetings.Parse(Calendar(Event(start:"20251022T110000Z",end:"20251022T120000Z")),Now).Next);
        var unsupported=CalendarMeetings.Parse(Calendar(Event("RRULE:FREQ=MONTHLY\r\n")),Now);Assert.Null(unsupported.Next);Assert.Contains("skipped",unsupported.Status);
    }
    [Fact] public void DailyRecurrenceSkipsExcludedDatesAndRespectsCount()
    {
        var value=CalendarMeetings.Parse(Calendar(Event("RRULE:FREQ=DAILY;COUNT=3\r\nEXDATE:20261022T110000Z\r\n", "20261021T110000Z","20261021T120000Z")),Now);
        Assert.Equal(DateTimeOffset.Parse("2026-10-23T11:00:00Z"),value.Next?.Start);
        Assert.Null(CalendarMeetings.Parse(Calendar(Event("RRULE:FREQ=DAILY;COUNT=1\r\n","20261021T110000Z","20261021T120000Z")),Now).Next);
    }
    [Fact] public void CalendarSupportsExplicitDurationWithoutInventingOne()
    {
        var timed=Calendar(Event("DURATION:PT30M\r\n").Replace("DTEND:20261022T120000Z\r\n",""));
        var meeting=CalendarMeetings.Parse(timed,Now).Next;Assert.NotNull(meeting);Assert.Equal(TimeSpan.FromMinutes(30),meeting.End-meeting.Start);
        var instant=Calendar(Event().Replace("DTEND:20261022T120000Z\r\n",""));
        var point=CalendarMeetings.Parse(instant,Now).Next;Assert.NotNull(point);Assert.Equal(point.Start,point.End);
    }
    [Fact] public void WeeklyMeetingsRespectTimezoneAndEditedOccurrences()
    {
        var text=Calendar(Event("RRULE:FREQ=WEEKLY;BYDAY=TH\r\n","20261015T110000Z","20261015T120000Z")+
            Event("RECURRENCE-ID:20261022T110000Z\r\n","20261022T130000Z","20261022T140000Z"));
        Assert.Equal(DateTimeOffset.Parse("2026-10-22T13:00:00Z"),CalendarMeetings.Parse(text,Now).Next?.Start);
        var timezone=Calendar(Event().Replace("DTSTART:20261022T110000Z","DTSTART;TZID=Europe/Paris:20261022T130000").Replace("DTEND:20261022T120000Z","DTEND;TZID=Europe/Paris:20261022T140000"));
        Assert.Equal(DateTimeOffset.Parse("2026-10-22T11:00:00Z"),CalendarMeetings.Parse(timezone,Now).Next?.Start.ToUniversalTime());
    }
    private sealed class Handler : HttpMessageHandler
    {
        public List<HttpRequestMessage> Requests {get;}=[];
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token)
        {
            Requests.Add(request);var payload=request.RequestUri!.AbsolutePath=="/notifications" ? "[{\"unread\":true}]" : "{\"workflow_runs\":[{\"status\":\"completed\",\"conclusion\":\"success\",\"html_url\":\"https://github.com/me/repo/actions/runs/1\"}]}";
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK){Content=new StringContent(payload,Encoding.UTF8,"application/json")});
        }
    }
    [Fact] public async Task GitHubReadsRealEndpointShapesWithNoMutationsAndKeepsCredentialsInHeaders()
    {
        var handler=new Handler();using var client=new HttpClient(handler);var provider=new GitHubModeService(client);
        var result=await provider.SampleAsync(["me/repo"],"fixture-token",default);
        Assert.Equal(1,result.Unread);Assert.Equal("success",Assert.Single(result.Repositories).Status);
        Assert.All(handler.Requests,r=>{Assert.Equal(HttpMethod.Get,r.Method);Assert.Equal("fixture-token",r.Headers.Authorization?.Parameter);Assert.DoesNotContain("fixture-token",r.RequestUri!.ToString());});
    }
    [Fact] public async Task LocalCalendarFileLoadsWithoutAnAccount()
    {
        var path=Path.Combine(Path.GetTempPath(),Guid.NewGuid()+".ics");
        try{await File.WriteAllTextAsync(path,Calendar(Event()));var value=await new CalendarModeService().SampleAsync(path,Now,default);Assert.NotNull(value.Next);}
        finally{File.Delete(path);}
    }
}
