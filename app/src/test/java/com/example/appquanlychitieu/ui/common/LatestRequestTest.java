package com.example.appquanlychitieu.ui.common;

import static org.junit.Assert.assertFalse;
import static org.junit.Assert.assertTrue;

import org.junit.Test;

public class LatestRequestTest {
    @Test
    public void onlyNewestRequestRemainsCurrent() {
        LatestRequest requests = new LatestRequest();

        int january = requests.begin();
        int february = requests.begin();

        assertFalse(requests.isCurrent(january));
        assertTrue(requests.isCurrent(february));
    }
}
