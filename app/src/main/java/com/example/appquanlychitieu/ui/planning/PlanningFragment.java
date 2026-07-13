package com.example.appquanlychitieu.ui.planning;

import android.os.Bundle;
import android.view.LayoutInflater;
import android.view.View;
import android.view.ViewGroup;

import androidx.annotation.NonNull;
import androidx.annotation.Nullable;
import androidx.fragment.app.Fragment;
import androidx.viewpager2.widget.ViewPager2;

import com.example.appquanlychitieu.R;
import com.example.appquanlychitieu.ui.budget.BudgetFragment;
import com.example.appquanlychitieu.ui.goals.GoalFragment;
import com.google.android.material.floatingactionbutton.ExtendedFloatingActionButton;
import com.google.android.material.tabs.TabLayout;
import com.google.android.material.tabs.TabLayoutMediator;

public class PlanningFragment extends Fragment {
    public static final String RESULT_ADD = "planning_add";
    public static final String RESULT_TAB = "tab";
    private static final String STATE_TAB = "planning_selected_tab";

    private ViewPager2 viewPager;
    private ExtendedFloatingActionButton fabAdd;
    private int selectedTab;

    @Nullable
    @Override
    public View onCreateView(@NonNull LayoutInflater inflater, @Nullable ViewGroup container,
                             @Nullable Bundle savedInstanceState) {
        return inflater.inflate(R.layout.fragment_planning, container, false);
    }

    @Override
    public void onViewCreated(@NonNull View view, @Nullable Bundle savedInstanceState) {
        super.onViewCreated(view, savedInstanceState);
        TabLayout tabs = view.findViewById(R.id.tabs_planning);
        viewPager = view.findViewById(R.id.view_pager_planning);
        fabAdd = view.findViewById(R.id.fab_add_planning);
        viewPager.setAdapter(new PlanningPagerAdapter(this));
        viewPager.setOffscreenPageLimit(1);

        new TabLayoutMediator(tabs, viewPager, (tab, position) -> tab.setText(
                position == PlanningPagerAdapter.PAGE_BUDGET
                        ? R.string.planning_budget_tab : R.string.planning_goal_tab)).attach();

        int initial = getArguments() == null ? 0 : getArguments().getInt("initialTab", 0);
        selectedTab = savedInstanceState == null
                ? Math.max(0, Math.min(1, initial))
                : savedInstanceState.getInt(STATE_TAB, initial);
        viewPager.setCurrentItem(selectedTab, false);
        updateFab(selectedTab);

        viewPager.registerOnPageChangeCallback(new ViewPager2.OnPageChangeCallback() {
            @Override
            public void onPageSelected(int position) {
                selectedTab = position;
                updateFab(position);
            }
        });
        fabAdd.setOnClickListener(v -> dispatchAddRequest());
    }

    private void dispatchAddRequest() {
        Fragment currentPage = getChildFragmentManager()
                .findFragmentByTag("f" + selectedTab);
        if (currentPage instanceof BudgetFragment) {
            ((BudgetFragment) currentPage).showAddBudgetDialog();
            return;
        }
        if (currentPage instanceof GoalFragment) {
            ((GoalFragment) currentPage).showAddGoalDialog();
            return;
        }

        // Keep a lifecycle-aware fallback for the short interval while ViewPager2
        // is still creating the selected page.
        Bundle result = new Bundle();
        result.putInt(RESULT_TAB, selectedTab);
        getChildFragmentManager().setFragmentResult(RESULT_ADD, result);
    }

    private void updateFab(int tab) {
        boolean goals = tab == PlanningPagerAdapter.PAGE_GOALS;
        fabAdd.setText(goals ? R.string.add_goal : R.string.add_budget);
        fabAdd.setContentDescription(getString(goals ? R.string.add_goal : R.string.add_budget));
    }

    @Override
    public void onSaveInstanceState(@NonNull Bundle outState) {
        super.onSaveInstanceState(outState);
        outState.putInt(STATE_TAB, selectedTab);
    }
}
