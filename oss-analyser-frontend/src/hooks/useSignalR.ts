import { useEffect, useMemo, useState } from 'react';
import * as signalR from '@microsoft/signalr';

export interface AlertMessage {
  type: string;
  repositoryId?: string;
  repositoryName?: string;
  cveId?: string;
  title?: string;
  severity?: string;
  riskScore?: number;
  timestamp?: string;
}

const useSignalR = () => {
  const [alerts, setAlerts] = useState<AlertMessage[]>([]);
  const [connected, setConnected] = useState(false);

  const connection = useMemo(() => new signalR.HubConnectionBuilder()
    .withUrl('http://localhost:5000/hubs/alerts', { withCredentials: false })
    .withAutomaticReconnect()
    .build(), []);

  useEffect(() => {
    const startConnection = async () => {
      try {
        await connection.start();
        setConnected(true);
        connection.on('ReceiveAlert', (message: AlertMessage) => {
          setAlerts((prev) => [message, ...prev].slice(0, 5));
        });
      } catch (error) {
        console.error('SignalR connection failed', error);
      }
    };

    startConnection();

    return () => {
      connection.stop();
    };
  }, [connection]);

  return { alerts, connected };
};

export default useSignalR;
