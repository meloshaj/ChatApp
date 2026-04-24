# Object-Oriented Design Patterns Implementation

## Overview
This enhanced chat application demonstrates comprehensive OOP principles and design patterns for real-world scalability.

---

## 1️⃣ Object-Oriented Design Principles

### Single Responsibility Principle (SRP)
Each class has one clear responsibility:
- `User` - Manages user data and state
- `Message` - Represents message data
- `Conversation` - Manages conversation participants and messages
- `AuthenticationService` - Handles authentication only
- `ChatService` - Manages message routing
- `NotificationService` - Handles real-time notifications

### Open/Closed Principle (OCP)
- `IMessageProcessor` interface allows adding new processors without modifying existing code
- `IUserState` interface enables new status types without changing User class
- Repository pattern allows swapping data stores (InMemory → SQLite → SQL Server)

### Liskov Substitution Principle (LSP)
- `TextMessage`, `ImageMessage`, `VoiceMessage` can substitute `Message`
- `PrivateConversation`, `GroupConversation` can substitute `Conversation`
- All repository implementations can substitute `IRepository<T>`

### Interface Segregation Principle (ISP)
- Separate interfaces: `IUserRepository`, `IMessageRepository`, `IConversationRepository`
- Clients depend only on methods they use

### Dependency Inversion Principle (DIP)
- High-level modules depend on abstractions (interfaces), not concrete implementations
- `EnhancedChatService` depends on `IMessageRepository`, not `InMemoryMessageRepository`

---

## 2️⃣ Design Patterns Implemented

### 1. Repository Pattern
**Purpose**: Abstract data access layer

**Implementation**:
```
ChatCore/Interfaces/
  - IRepository<T>
  - IUserRepository
  - IMessageRepository
  - IConversationRepository

ChatCore/Repositories/
  - InMemoryUserRepository
  - InMemoryMessageRepository
  - InMemoryConversationRepository
```

**Benefits**:
- Easy to swap data stores (add SQLiteRepository, SqlServerRepository)
- Testable with mock repositories
- Centralized data access logic

---

### 2. State Pattern
**Purpose**: User status behavior changes based on state

**Implementation**:
```
ChatCore/States/
  - IUserState (interface)
  - OnlineState
  - AwayState
  - BusyState
  - OfflineState
```

**Usage**:
```csharp
user.SetState(new BusyState());
if (user.GetState().ShouldSendAutoReply()) {
    // Send auto-reply
}
```

**Benefits**:
- Different behavior per status (auto-replies, message queuing)
- Easy to add new states
- Encapsulates state-specific logic

---

### 3. Factory Pattern
**Purpose**: Centralized message creation

**Implementation**:
```
ChatCore/Factories/
  - IMessageFactory
  - MessageFactory
```

**Usage**:
```csharp
var message = messageFactory.CreateTextMessage(userId, conversationId, text);
var imageMsg = messageFactory.CreateImageMessage(userId, conversationId, fileName, data);
```

**Benefits**:
- Consistent message creation
- Easy to add new message types
- Encapsulates creation logic

---

### 4. Command Pattern
**Purpose**: Encapsulate operations with undo capability

**Implementation**:
```
ChatCore/Commands/
  - ICommand
  - EditMessageCommand
  - DeleteMessageCommand
  - CommandHistory
```

**Usage**:
```csharp
var editCommand = new EditMessageCommand(repository, messageId, newText);
await commandHistory.ExecuteAsync(editCommand);
await commandHistory.UndoAsync(); // Undo the edit
```

**Benefits**:
- Undo/redo functionality
- Operation history tracking
- Decouples invoker from executor

---

### 5. Observer Pattern
**Purpose**: Real-time notifications for typing, presence, messages

**Implementation**:
```
ChatCore/Observers/
  - IObserver<T>
  - ISubject<T>
  - TypingNotification
  - PresenceNotification
  
ChatCore/Services/
  - NotificationService
```

**Usage**:
```csharp
notificationService.Attach(typingObserver);
await notificationService.NotifyAsync(new TypingNotification {
    UserId = user.Id,
    IsTyping = true
});
```

**Benefits**:
- Decoupled notification system
- Multiple observers can subscribe
- Real-time updates

---

### 6. Decorator Pattern
**Purpose**: Add responsibilities to message processing dynamically

**Implementation**:
```
ChatCore/Decorators/
  - ValidationMessageProcessor
  - LoggingMessageProcessor
  - SpamFilterProcessor
```

**Usage**:
```csharp
var badWordFilter = new BadWordFilter();
var validationProcessor = new ValidationMessageProcessor(badWordFilter);
var loggingProcessor = new LoggingMessageProcessor(validationProcessor);
var spamFilter = new SpamFilterProcessor(loggingProcessor);

chatService.AddProcessor(spamFilter);
```

**Benefits**:
- Flexible processing pipeline
- Add/remove processors at runtime
- Single Responsibility - each decorator has one job

---

## 3️⃣ Authentication & User Management

### AuthenticationService
**Features**:
- User registration with password hashing (SHA256)
- Login with credential validation
- Session token management
- Secure password storage

**Usage**:
```csharp
var (success, userId, token, error) = await authService.RegisterAsync(username, password);
var (success, userId, token, error) = await authService.LoginAsync(username, password);
var user = await authService.ValidateSessionAsync(token);
```

**Security**:
- Passwords never stored in plain text
- SHA256 hashing with salt
- Session-based authentication

---

## 4️⃣ Message Persistence & Loading

### Message Repository
**Features**:
- Store messages persistently
- Load conversation history
- Track read receipts
- Query messages by conversation

**Usage**:
```csharp
await messageRepository.AddAsync(message);
var history = await messageRepository.GetConversationMessagesAsync(conversationId, limit: 50);
await messageRepository.MarkAsReadAsync(messageId, userId);
```

**Benefits**:
- Messages survive server restart (with proper repository implementation)
- Efficient history loading
- Read receipt tracking

---

## 5️⃣ Real-Time Communication

### Typing Indicators
```csharp
await chatService.NotifyTypingAsync(user, conversationId, isTyping: true);
```

### User Presence
```csharp
user.SetState(new AwayState());
await NotifyPresenceChangeAsync(user, UserStatus.Away);
```

### Message Notifications
- Automatic notification to all conversation participants
- Observer pattern ensures all subscribers receive updates
- Network failures handled gracefully

---

## Architecture Diagram

```
┌─────────────────────────────────────────────────────────────┐
│                         Client Layer                         │
│  (WPF UI, User Interaction, Display Logic)                  │
└─────────────────────────────────────────────────────────────┘
                            ↓
┌─────────────────────────────────────────────────────────────┐
│                      Service Layer                           │
│  ┌──────────────────┐  ┌──────────────────┐                │
│  │ Authentication   │  │ EnhancedChat     │                │
│  │ Service          │  │ Service          │                │
│  └──────────────────┘  └──────────────────┘                │
│  ┌──────────────────┐  ┌──────────────────┐                │
│  │ Notification     │  │ Command          │                │
│  │ Service          │  │ Service          │                │
│  └──────────────────┘  └──────────────────┘                │
└─────────────────────────────────────────────────────────────┘
                            ↓
┌─────────────────────────────────────────────────────────────┐
│                    Repository Layer                          │
│  ┌──────────────────┐  ┌──────────────────┐                │
│  │ User             │  │ Message          │                │
│  │ Repository       │  │ Repository       │                │
│  └──────────────────┘  └──────────────────┘                │
│  ┌──────────────────┐                                       │
│  │ Conversation     │                                       │
│  │ Repository       │                                       │
│  └──────────────────┘                                       │
└─────────────────────────────────────────────────────────────┘
                            ↓
┌─────────────────────────────────────────────────────────────┐
│                      Data Layer                              │
│  (In-Memory, SQLite, SQL Server, etc.)                      │
└─────────────────────────────────────────────────────────────┘
```

---

## Message Processing Pipeline

```
User Input
    ↓
[SpamFilterProcessor]
    ↓
[LoggingProcessor]
    ↓
[ValidationProcessor]
    ↓
[BadWordFilter]
    ↓
Message Saved to Repository
    ↓
Broadcast to Participants
```

---

## How to Run Enhanced Server

1. Build the solution:
```bash
dotnet build
```

2. Run the enhanced server:
```bash
dotnet run --project ChatServer/EnhancedProgram.cs
```

3. Connect with client and authenticate:
```
REGISTER username password
# or
LOGIN username password
```

---

## New Commands

### User Management
- `REGISTER username password` - Create new account
- `LOGIN username password` - Login to existing account
- `/status <online|away|busy>` - Change status

### Message Operations
- `/edit <messageId> <newText>` - Edit your message
- `/delete <messageId>` - Delete your message
- `/undo` - Undo last edit/delete

### Existing Commands
- `/users` - List all users with status
- `/friends` - List friends with status
- `/friend add <username>` - Add friend
- `/chat <username>` - Open private chat
- `/group create <name>` - Create group
- `/group add <name> <username>` - Add user to group
- `/group chat <name>` - Open group chat

---

## Testing Scenarios

### 1. Authentication
- Register new user
- Login with correct credentials
- Login with wrong credentials
- Multiple sessions

### 2. Message Persistence
- Send messages
- Disconnect and reconnect
- Verify message history loads

### 3. Real-Time Features
- Start typing → others see typing indicator
- Change status → others see presence update
- Send message → instant delivery

### 4. Command Pattern
- Edit a message
- Delete a message
- Undo the operation

### 5. State Pattern
- Set status to Busy → auto-reply sent
- Set status to Away → auto-reply sent
- Set status to Online → no auto-reply

---

## Extensibility

### Adding New Message Type
1. Create class extending `Message`
2. Add factory method in `MessageFactory`
3. Handle in `EnhancedChatService`

### Adding New Repository
1. Implement `IUserRepository`, `IMessageRepository`, `IConversationRepository`
2. Swap in `EnhancedProgram.InitializeServices()`

### Adding New Processor
1. Implement `IMessageProcessor`
2. Wrap existing processor (decorator pattern)
3. Add to pipeline in `InitializeServices()`

### Adding New User State
1. Implement `IUserState`
2. Define behavior in methods
3. Use with `user.SetState(new CustomState())`

---

## Summary

This implementation demonstrates:
✅ SOLID principles throughout
✅ 6+ design patterns (Repository, State, Factory, Command, Observer, Decorator)
✅ Authentication with secure password storage
✅ Message persistence and history loading
✅ Real-time communication (typing, presence, messages)
✅ Extensible architecture
✅ Production-ready patterns
✅ Testable design with dependency injection
