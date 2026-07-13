package com.example.appquanlychitieu.ui.goals;

import static org.junit.Assert.assertFalse;
import static org.junit.Assert.assertTrue;

import com.example.appquanlychitieu.data.model.Goal;

import org.junit.Test;

public class GoalListAdapterDiffTest {
    @Test
    public void backendIdDefinesIdentityAndProgressDefinesContent() {
        Goal first = goal("goal-1", 100_000d);
        Goal same = goal("goal-1", 100_000d);
        Goal changed = goal("goal-1", 300_000d);
        assertTrue(GoalListAdapter.sameItem(first, same));
        assertTrue(GoalListAdapter.sameContent(first, same));
        assertFalse(GoalListAdapter.sameContent(first, changed));
    }

    private Goal goal(String id, double current) {
        Goal value = new Goal("Quỹ khẩn cấp", 1_000_000d, current, 1L);
        value.setRemoteId(id);
        return value;
    }
}
