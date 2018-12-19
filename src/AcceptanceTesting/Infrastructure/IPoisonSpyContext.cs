public interface IPoisonSpyContext
{
    string ExceptionMessage { get; set; }
    bool PoisonMessageDetected { get; set; }
}