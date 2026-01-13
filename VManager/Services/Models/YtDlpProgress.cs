namespace VManager.Services.Models;

public class YtDlpProgress
{
    public double Progress { get; }
    public string Speed { get; }
    public string Eta { get; }

    public YtDlpProgress(double progress, string speed, string eta)
    {
        Progress = progress;
        Speed = speed;
        Eta = eta;
    }
}