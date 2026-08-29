namespace MotionRunner.Commerce
{
    /// Why a store call failed, in plain data. Code is stable enough to log and branch on;
    /// Message is for humans (logcat / on-screen status), already localized where the SDK does.
    public sealed class StoreError
    {
        public string Code { get; }
        public string Message { get; }

        public StoreError(string code, string message)
        {
            Code = code ?? "unknown";
            Message = message ?? string.Empty;
        }

        public override string ToString() => Code + ": " + Message;
    }
}
