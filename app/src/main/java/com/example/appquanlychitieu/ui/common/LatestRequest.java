package com.example.appquanlychitieu.ui.common;

/** Issues monotonically increasing request ids and rejects stale callbacks. */
public final class LatestRequest {
    private int generation;

    public int begin() {
        return ++generation;
    }

    public boolean isCurrent(int requestGeneration) {
        return requestGeneration == generation;
    }
}
