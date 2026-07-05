import { createContext, useContext, useEffect, useState, type ReactNode } from 'react';
import * as signalR from '@microsoft/signalr';
import { useAuth } from './AuthContext';

interface WarehouseEvent {
  type: string;
  [key: string]: any;
}

interface SignalRContextType {
  isConnected: boolean;
}

const SignalRContext = createContext<SignalRContextType | undefined>(undefined);

export function SignalRProvider({ children }: { children: ReactNode }) {
  const { token } = useAuth();
  const [isConnected, setIsConnected] = useState(false);

  useEffect(() => {
    if (!token) return;

    const connection = new signalR.HubConnectionBuilder()
      .withUrl('/hubs/warehouse', {
        accessTokenFactory: () => token
      })
      .withAutomaticReconnect()
      .build();

    connection.start()
      .then(() => {
        setIsConnected(true);
        console.log('SignalR Connected');
      })
      .catch(err => console.error('SignalR Connection Error: ', err));

    connection.on('WarehouseEvent', (event: WarehouseEvent) => {
      console.log('Warehouse Event received:', event);
      // In a real app, we would use a state management library (Redux/Zustand)
      // or a custom event emitter to notify specific components.
      // For now, we will use browser notifications for critical events.
      if (['PICKING_STARTED', 'INVENTORY_STARTED', 'ALERT_CREATED'].includes(event.type)) {
        if (Notification.permission === 'granted') {
           new Notification(`WMS: ${event.type}`, { body: event.name || event.message || '' });
        }
      }
    });

    return () => {
      connection.stop();
    };
  }, [token]);

  useEffect(() => {
    if (Notification.permission === 'default') {
      Notification.requestPermission();
    }
  }, []);

  return (
    <SignalRContext.Provider value={{ isConnected }}>
      {children}
    </SignalRContext.Provider>
  );
}

export const useSignalR = () => {
  const context = useContext(SignalRContext);
  if (context === undefined) {
    throw new Error('useSignalR must be used within a SignalRProvider');
  }
  return context;
};
