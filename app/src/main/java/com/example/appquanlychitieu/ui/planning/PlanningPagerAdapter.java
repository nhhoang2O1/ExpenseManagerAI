package com.example.appquanlychitieu.ui.planning;

import androidx.annotation.NonNull;
import androidx.fragment.app.Fragment;
import androidx.viewpager2.adapter.FragmentStateAdapter;

import com.example.appquanlychitieu.ui.budget.BudgetFragment;
import com.example.appquanlychitieu.ui.goals.GoalFragment;

final class PlanningPagerAdapter extends FragmentStateAdapter {
    static final int PAGE_BUDGET = 0;
    static final int PAGE_GOALS = 1;

    PlanningPagerAdapter(@NonNull Fragment fragment) {
        super(fragment);
    }

    @NonNull
    @Override
    public Fragment createFragment(int position) {
        return position == PAGE_GOALS ? new GoalFragment() : new BudgetFragment();
    }

    @Override
    public int getItemCount() {
        return 2;
    }
}
