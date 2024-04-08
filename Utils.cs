namespace HidamariBot;

public static class Utils {
    static readonly Random random = new();
    static readonly object syncLock = new();
    public static int RandomNumber(int min, int excludedMax) {
        lock (syncLock) { // synchronize
            return random.Next(min, excludedMax);
        }
    }

    public static uint RandomNumber(uint min, uint excludedMax)
        => Convert.ToUInt32(RandomNumber(Convert.ToInt32(min), Convert.ToInt32(excludedMax)));
}
