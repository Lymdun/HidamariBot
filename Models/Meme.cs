namespace HidamariBot.Models;

public struct Meme {
    public DayOfWeek DayOfWeek;
    public string ImageUrl;

    Meme(DayOfWeek dayOfWeek, string imageUrl) {
        this.DayOfWeek = dayOfWeek;
        this.ImageUrl = imageUrl;
    }

    public static Meme MondayMeme() {
        return new Meme(DayOfWeek.Monday, "makise_monday.webm");
    }

    public static Meme TuesdayMeme() {
        return new Meme(DayOfWeek.Tuesday, "everybody.webm");
    }
}
