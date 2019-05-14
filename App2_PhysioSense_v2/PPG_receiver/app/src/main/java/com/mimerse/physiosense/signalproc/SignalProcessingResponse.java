package com.mimerse.physiosense.signalproc;

public interface SignalProcessingResponse <T> {
    public void OnProcessFinished(T object);
}