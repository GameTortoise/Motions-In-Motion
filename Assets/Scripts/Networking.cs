using System;
using System.Collections.Generic;
using PurrNet;
using UnityEngine;

public class NetworkEventBus : NetworkBehaviour
{
    private readonly Dictionary<string, List<Action<object>>> _handlers = new();

    public void On<T>(string eventName, Action<T> callback)
    {
        if (!_handlers.ContainsKey(eventName))
            _handlers[eventName] = new List<Action<object>>();

        _handlers[eventName].Add(obj =>
        {
            if (obj is T typedData)
                callback(typedData);
        });
    }

    public void SendFromHost<T>(string eventName, T data)
    {
        if (!isServer)
            return;

        ReceiveBroadcastRpc(eventName, data);
    }

    public void SendToHost<T>(string eventName, T data)
    {
        if (!isClient)
            return;

        ReceiveOnHostRpc(eventName, data);
    }

    [ObserversRpc(runLocally: false)]
    private void ReceiveBroadcastRpc<T>(string eventName, T data)
    {
        Dispatch(eventName, data);
    }

    [ServerRpc(requireOwnership: false)]
    private void ReceiveOnHostRpc<T>(string eventName, T data)
    {
        Dispatch(eventName, data);
    }

    private void Dispatch<T>(string eventName, T data)
    {
        if (_handlers.TryGetValue(eventName, out var listeners))
        {
            for (int i = 0; i < listeners.Count; i++)
            {
                listeners[i].Invoke(data);
            }
        }
    }
}
