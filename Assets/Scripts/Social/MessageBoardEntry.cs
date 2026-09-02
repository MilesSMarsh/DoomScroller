namespace DoomScroller.Social
{
    // One line in a completed encounter's minimal comment list (Phase 7 stub).
    // Threading/replies/moderation are explicitly out of scope.
    public readonly struct MessageBoardEntry
    {
        public readonly string Author;
        public readonly string Text;

        public MessageBoardEntry(string author, string text)
        {
            Author = author;
            Text = text;
        }
    }
}
