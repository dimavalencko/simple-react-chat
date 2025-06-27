import React, { useState, useEffect, useRef } from 'react';
import './index.css';

interface ChatMessage {
  user: string;
  text: string;
  timestamp: string;
}

const ChatApp: React.FC = () => {
  const [messages, setMessages] = useState<ChatMessage[]>([]);
  const [messageInput, setMessageInput] = useState('');
  const [user, setUser] = useState('');
  const [serverAddress, setServerAddress] = useState('');
  const [connected, setConnected] = useState(false);
  const ws = useRef<WebSocket | null>(null);

  const connectToServer = () => {
    if (!user || !serverAddress) return;

    const socket = new WebSocket(`ws://${serverAddress}/chat`);
    ws.current = socket;

    socket.onopen = () => {
      setConnected(true);
      socket.send(JSON.stringify({ User: user }));
    };

    socket.onmessage = (event) => {
      const message = JSON.parse(event.data);
      setMessages(prev => [...prev, {
        user: message.User,
        text: message.Text,
        timestamp: new Date(message.Timestamp).toLocaleTimeString()
      }]);
    };

    socket.onclose = () => {
      setConnected(false);
    };
  };

  const sendMessage = () => {
    if (messageInput && ws.current) {
      ws.current.send(JSON.stringify({
        User: user,
        Text: messageInput
      }));
      setMessageInput('');
    }
  };

  const handleKeyPress = (e: React.KeyboardEvent) => {
    if (e.key === 'Enter') {
      sendMessage();
    }
  };

  useEffect(() => {
    return () => {
      if (ws.current) {
        ws.current.close();
      }
    };
  }, []);

  if (!connected) {
    return (
      <div className="login-container">
        <h2>Вход в чат</h2>
        <input
          type="text"
          placeholder="Адрес сервера (ip:port)"
          value={serverAddress}
          onChange={(e) => setServerAddress(e.target.value)}
        />
        <input
          type="text"
          placeholder="Ваш логин"
          value={user}
          onChange={(e) => setUser(e.target.value)}
        />
        <button onClick={connectToServer}>Подключиться</button>
      </div>
    );
  }

  return (
    <div className="chat-container">
      <div className="chat-header">
        <h2>Чат ({user})</h2>
        <span>Подключено к {serverAddress}</span>
      </div>
      
      <div className="chat-messages">
        {messages.map((msg, index) => (
        <div 
          key={index} 
          className={`message ${msg.user === user ? 'mine' : 'theirs'}`}
        >
          <span className="message-user">{msg.user}</span>
          <span className="message-time">{msg.timestamp}</span>
          <div className="message-text">{msg.text}</div>
        </div>
      ))}
      </div>
      
      <div className="chat-input">
        <input
          type="text"
          value={messageInput}
          onChange={(e) => setMessageInput(e.target.value)}
          onKeyPress={handleKeyPress}
          placeholder="Введите сообщение..."
        />
        <button onClick={sendMessage}>Отправить</button>
      </div>
    </div>
  );
};

export default ChatApp;