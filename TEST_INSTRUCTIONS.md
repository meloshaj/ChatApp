# Test Instructions for Chat Application

## Setup
1. Start ChatServer: `dotnet run --project ChatServer/ChatServer.csproj`
2. Start ChatClient 1: `dotnet run --project ChatClient.Wpf/ChatClient.Wpf.csproj`
3. Start ChatClient 2: `dotnet run --project ChatClient.Wpf/ChatClient.Wpf.csproj`

## Test Scenario

### Client 1:
1. Connect with username "Alice"
2. Click "Refresh" to see online users
3. Type "Bob" in the friend textbox
4. Click "Add Friend"
5. Click "Open Chat"
6. **IMPORTANT**: Check the status message - should say "Chat selected: Bob"
7. Click in the message input textbox (the large one at the bottom)
8. Type "Hello Bob"
9. Verify you can SEE the text "Hello Bob" in the textbox as you type
10. Press Enter or click Send
11. Verify "Hello Bob" appears in YOUR chat window
12. Verify it appears as a blue bubble on the right side

### Client 2:
1. Connect with username "Bob"
2. Click "Refresh"
3. Type "Alice" in friend textbox
4. Click "Add Friend"
5. Click "Open Chat"
6. You should see "Hello Bob" appear as a gray bubble on the left

### Expected Behavior:
- Text should be visible while typing in the message input box
- Messages should appear immediately in sender's chat
- Messages should appear in recipient's chat
- No duplicate messages
- No command text (like "/chat Alice") should appear in chat

### Common Issues to Check:
1. **Can't see text while typing**: Check if you're clicking in the correct textbox (the large one at bottom, not the image/voice path boxes above it)
2. **"/userA" appearing in chat**: This suggests the conversation wasn't properly selected before sending
3. **Nothing happens when pressing Send**: Check if "Chat selected: username" message appeared in status
4. **Duplicate messages**: Should be fixed now

### Debug Info:
- Status messages appear in the gray box at top right
- Current conversation name appears in the header
- Send button should only be enabled when a conversation is selected
