package com.example.appquanlychitieu.ui.goals;

import android.os.Bundle;
import android.view.LayoutInflater;
import android.view.View;
import android.view.ViewGroup;

import androidx.annotation.NonNull;
import androidx.annotation.Nullable;
import androidx.appcompat.app.AlertDialog;
import androidx.fragment.app.Fragment;
import androidx.lifecycle.ViewModelProvider;
import androidx.navigation.fragment.NavHostFragment;
import androidx.recyclerview.widget.LinearLayoutManager;
import androidx.recyclerview.widget.RecyclerView;

import com.example.appquanlychitieu.R;
import com.example.appquanlychitieu.data.model.Goal;
import com.example.appquanlychitieu.ui.common.LoadState;
import com.example.appquanlychitieu.ui.planning.PlanningFragment;
import com.example.appquanlychitieu.util.NumberTextWatcher;
import com.google.android.material.dialog.MaterialAlertDialogBuilder;
import com.google.android.material.snackbar.Snackbar;
import com.google.android.material.textfield.TextInputEditText;
import com.google.android.material.textfield.TextInputLayout;

public class GoalFragment extends Fragment implements GoalListAdapter.OnGoalInteractionListener {
    private GoalViewModel viewModel;
    private GoalListAdapter adapter;
    private RecyclerView goalsView;
    private View emptyState;
    private View errorState;
    private View loading;

    @Nullable
    @Override
    public View onCreateView(@NonNull LayoutInflater inflater, @Nullable ViewGroup container,
                             @Nullable Bundle savedInstanceState) {
        return inflater.inflate(R.layout.fragment_goals, container, false);
    }

    @Override
    public void onViewCreated(@NonNull View view, @Nullable Bundle savedInstanceState) {
        super.onViewCreated(view, savedInstanceState);
        goalsView = view.findViewById(R.id.rv_goals);
        emptyState = view.findViewById(R.id.layout_empty_state);
        errorState = view.findViewById(R.id.layout_error_state);
        loading = view.findViewById(R.id.progress_loading);
        adapter = new GoalListAdapter(requireContext(), this);
        goalsView.setLayoutManager(new LinearLayoutManager(requireContext()));
        goalsView.setAdapter(adapter);
        viewModel = new ViewModelProvider(this).get(GoalViewModel.class);

        view.findViewById(R.id.btn_empty_cta).setOnClickListener(v -> showAddGoalDialog());
        view.findViewById(R.id.btn_retry).setOnClickListener(v -> viewModel.refreshGoals());
        getParentFragmentManager().setFragmentResultListener(
                PlanningFragment.RESULT_ADD, getViewLifecycleOwner(), (key, result) -> {
                    if (result.getInt(PlanningFragment.RESULT_TAB, -1) == 1) showAddGoalDialog();
                });

        viewModel.getGoals().observe(getViewLifecycleOwner(), adapter::setGoals);
        viewModel.getLoadState().observe(getViewLifecycleOwner(), this::renderState);
        viewModel.getError().observe(getViewLifecycleOwner(), message -> {
            if (message != null && !message.trim().isEmpty())
                Snackbar.make(view, message, Snackbar.LENGTH_LONG).show();
        });
        viewModel.getFeedback().observe(getViewLifecycleOwner(), message -> {
            if (message != null && !message.trim().isEmpty())
                Snackbar.make(view, message, Snackbar.LENGTH_SHORT).show();
        });
    }

    private void renderState(LoadState state) {
        loading.setVisibility(state == LoadState.LOADING ? View.VISIBLE : View.GONE);
        errorState.setVisibility(state == LoadState.ERROR ? View.VISIBLE : View.GONE);
        emptyState.setVisibility(state == LoadState.EMPTY ? View.VISIBLE : View.GONE);
        goalsView.setVisibility(state == LoadState.CONTENT ? View.VISIBLE : View.GONE);
    }

    public void showAddGoalDialog() {
        View content = LayoutInflater.from(requireContext()).inflate(R.layout.dialog_goal_amount, null);
        TextInputLayout nameLayout = content.findViewById(R.id.layout_goal_name);
        TextInputLayout amountLayout = content.findViewById(R.id.layout_goal_amount);
        TextInputEditText name = content.findViewById(R.id.et_goal_name);
        TextInputEditText amount = content.findViewById(R.id.et_goal_amount);
        amount.addTextChangedListener(new NumberTextWatcher(amount));
        AlertDialog dialog = new MaterialAlertDialogBuilder(requireContext())
                .setTitle(R.string.add_goal)
                .setView(content)
                .setPositiveButton(R.string.save, null)
                .setNegativeButton(R.string.cancel, null)
                .create();
        dialog.setOnShowListener(ignored -> dialog.getButton(AlertDialog.BUTTON_POSITIVE)
                .setOnClickListener(v -> {
                    String goalName = text(name);
                    double target = parseAmount(amount);
                    if (goalName.isEmpty()) {
                        nameLayout.setError(getString(R.string.invalid_name));
                        return;
                    }
                    if (target <= 0) {
                        amountLayout.setError(getString(R.string.amount_must_be_positive));
                        return;
                    }
                    viewModel.insertGoal(new Goal(goalName, target, 0d, viewModel.getUserId()));
                    dialog.dismiss();
                }));
        dialog.show();
    }

    @Override
    public void onAddFundsClick(Goal goal) {
        View content = LayoutInflater.from(requireContext()).inflate(R.layout.dialog_goal_amount, null);
        TextInputLayout nameLayout = content.findViewById(R.id.layout_goal_name);
        TextInputLayout amountLayout = content.findViewById(R.id.layout_goal_amount);
        TextInputEditText amount = content.findViewById(R.id.et_goal_amount);
        nameLayout.setVisibility(View.GONE);
        amountLayout.setHint(R.string.fund_amount_hint);
        amount.addTextChangedListener(new NumberTextWatcher(amount));
        AlertDialog dialog = new MaterialAlertDialogBuilder(requireContext())
                .setTitle(R.string.add_funds)
                .setView(content)
                .setPositiveButton(R.string.save, null)
                .setNegativeButton(R.string.cancel, null)
                .create();
        dialog.setOnShowListener(ignored -> dialog.getButton(AlertDialog.BUTTON_POSITIVE)
                .setOnClickListener(v -> {
                    double value = parseAmount(amount);
                    if (value <= 0) {
                        amountLayout.setError(getString(R.string.amount_must_be_positive));
                        return;
                    }
                    viewModel.addFunds(goal, value);
                    dialog.dismiss();
                }));
        dialog.show();
    }

    @Override
    public void onGoalClick(Goal goal) {
        Bundle args = new Bundle();
        args.putLong("goalId", goal.getId());
        args.putString("remoteGoalId", goal.getRemoteId());
        args.putString("goalName", goal.getName());
        NavHostFragment.findNavController(this).navigate(R.id.navigation_goal_history, args);
    }

    @Override
    public void onGoalLongClick(Goal goal) {
        new MaterialAlertDialogBuilder(requireContext())
                .setTitle(R.string.delete)
                .setMessage(R.string.confirm_delete)
                .setPositiveButton(R.string.delete, (dialog, which) -> viewModel.deleteGoal(goal))
                .setNegativeButton(R.string.cancel, null)
                .show();
    }

    private String text(TextInputEditText input) {
        return input.getText() == null ? "" : input.getText().toString().trim();
    }

    private double parseAmount(TextInputEditText input) {
        try { return Double.parseDouble(text(input).replace(".", "").replace(",", "")); }
        catch (RuntimeException ignored) { return 0d; }
    }
}
