import { HttpTransportType, HubConnection, HubConnectionBuilder, LogLevel } from '@microsoft/signalr';

const HUB_URL = (import.meta.env.VITE_API_URL || 'http://localhost:5000') + '/hub/warehouse';

class WarehouseSignalRService {
  private connection: HubConnection | null = null;
  private listeners: ((event: any) => void)[] = [];

  async start() {
    if (this.connection) return;

    const token = localStorage.getItem('token');
    if (!token) return;

    this.connection = new HubConnectionBuilder()
      .withUrl(HUB_URL, {
        accessTokenFactory: () => token,
        skipNegotiation: true,
        transport: HttpTransportType.WebSockets
      })
      .configureLogging(LogLevel.Information)
      .withAutomaticReconnect()
      .build();

    this.connection.on('WarehouseEvent', (event) => {
      this.listeners.forEach((l) => l(event));
    });

    try {
      await this.connection.start();
      console.log('SignalR connected');
    } catch (err) {
      console.error('SignalR connection error:', err);
    }
  }

  stop() {
    if (this.connection) {
      this.connection.stop();
      this.connection = null;
    }
  }

  onEvent(callback: (event: any) => void) {
    this.listeners.push(callback);
    return () => {
      this.listeners = this.listeners.filter((l) => l !== callback);
    };
  }
}

export const warehouseHub = new WarehouseSignalRService();
