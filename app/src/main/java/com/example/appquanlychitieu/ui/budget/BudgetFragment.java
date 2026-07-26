package com.example.appquanlychitieu.ui.budget;

import android.os.Bundle;
import android.view.LayoutInflater;
import android.view.View;
import android.view.ViewGroup;
import android.widget.ArrayAdapter;
import android.widget.TextView;

import androidx.annotation.NonNull;
import androidx.annotation.Nullable;
import androidx.appcompat.app.AlertDialog;
import androidx.fragment.app.Fragment;
import androidx.lifecycle.ViewModelProvider;
import androidx.recyclerview.widget.LinearLayoutManager;
import androidx.recyclerview.widget.RecyclerView;

import com.example.appquanlychitieu.R;
import com.example.appquanlychitieu.data.model.Budget;
import com.example.appquanlychitieu.data.remote.dto.CategoryDto;
import com.example.appquanlychitieu.ui.common.LoadState;
import com.example.appquanlychitieu.ui.planning.PlanningFragment;
import com.example.appquanlychitieu.util.CurrencyFormatter;
import com.example.appquanlychitieu.util.DateUtils;
import com.example.appquanlychitieu.util.NumberTextWatcher;
import com.google.android.material.dialog.MaterialAlertDialogBuilder;
import com.google.android.material.snackbar.Snackbar;
import com.google.android.material.textfield.MaterialAutoCompleteTextView;
import com.google.android.material.textfield.TextInputEditText;
import com.google.android.material.textfield.TextInputLayout;

import java.util.ArrayList;
import java.util.Calendar;
import java.util.HashMap;
import java.util.List;
import java.util.Map;

public class BudgetFragment extends Fragment {
    private BudgetViewModel viewModel;
    private BudgetListAdapter adapter;
    private final List<CategoryDto> categories = new ArrayList<>();
    private final Map<Long, Long> spentMap = new HashMap<>();
    private List<Budget> currentBudgets = new ArrayList<>();

    private RecyclerView budgetsView;
    private View emptyState;
    private View errorState;
    private View loading;
    private TextView currentMonth;
    private TextView totalBudget;
    private TextView totalSpent;

    @Nullable
    @Override
    public View onCreateView(@NonNull LayoutInflater inflater, @Nullable ViewGroup container,
                             @Nullable Bundle savedInstanceState) {
        return inflater.inflate(R.layout.fragment_budget, container, false);
    }

    @Override
    public void onViewCreated(@NonNull View view, @Nullable Bundle savedInstanceState) {
        super.onViewCreated(view, savedInstanceState);
        budgetsView = view.findViewById(R.id.rv_budgets);
        emptyState = view.findViewById(R.id.layout_empty_state);
        errorState = view.findViewById(R.id.layout_error_state);
        loading = view.findViewById(R.id.progress_loading);
        currentMonth = view.findViewById(R.id.tv_current_month);
        totalBudget = view.findViewById(R.id.tv_budget_total);
        totalSpent = view.findViewById(R.id.tv_budget_spent_total);

        adapter = new BudgetListAdapter(requireContext());
        budgetsView.setLayoutManager(new LinearLayoutManager(requireContext()));
        budgetsView.setAdapter(adapter);
        viewModel = new ViewModelProvider(this).get(BudgetViewModel.class);

        view.findViewById(R.id.btn_prev_month).setOnClickListener(v -> viewModel.previousMonth());
        view.findViewById(R.id.btn_next_month).setOnClickListener(v -> viewModel.nextMonth());
        view.findViewById(R.id.btn_empty_cta).setOnClickListener(v -> showAddBudgetDialog());
        view.findViewById(R.id.btn_retry).setOnClickListener(v -> viewModel.refreshBudgets());
        adapter.setListener(new BudgetListAdapter.Listener() {
            @Override public void onEdit(Budget budget) { showEditBudgetDialog(budget); }
            @Override public void onDelete(Budget budget) { confirmDelete(budget); }
        });

        getParentFragmentManager().setFragmentResultListener(
                PlanningFragment.RESULT_ADD, getViewLifecycleOwner(), (key, result) -> {
                    if (result.getInt(PlanningFragment.RESULT_TAB, -1) == 0) showAddBudgetDialog();
                });

        viewModel.getBudgets().observe(getViewLifecycleOwner(), budgets -> {
            currentBudgets = budgets == null ? new ArrayList<>() : budgets;
            adapter.setBudgets(currentBudgets);
            updateTotals();
        });
        viewModel.getLoadState().observe(getViewLifecycleOwner(), this::renderState);
        viewModel.getError().observe(getViewLifecycleOwner(), message -> {
            if (message != null && !message.trim().isEmpty() && isAdded()) {
                Snackbar.make(view, message, Snackbar.LENGTH_LONG).show();
            }
        });
        viewModel.getFeedback().observe(getViewLifecycleOwner(), message -> {
            if (message != null && !message.trim().isEmpty()) {
                Snackbar.make(view, message, Snackbar.LENGTH_SHORT).show();
            }
        });
        viewModel.getCategories().observe(getViewLifecycleOwner(), value -> {
            categories.clear();
            if (value != null) categories.addAll(value);
        });
        viewModel.getSpentByCategory().observe(getViewLifecycleOwner(), value -> {
            spentMap.clear();
            if (value != null) spentMap.putAll(value);
            adapter.setSpentMap(spentMap);
            updateTotals();
        });
        viewModel.getSelectedMonthYear().observe(getViewLifecycleOwner(), month -> {
            currentMonth.setText(DateUtils.formatDisplayMonth(
                    DateUtils.getStartOfMonth(month[0], month[1])));
            viewModel.loadSpent(month[0], month[1]);
        });
    }

    private void renderState(LoadState state) {
        loading.setVisibility(state == LoadState.LOADING ? View.VISIBLE : View.GONE);
        errorState.setVisibility(state == LoadState.ERROR ? View.VISIBLE : View.GONE);
        emptyState.setVisibility(state == LoadState.EMPTY ? View.VISIBLE : View.GONE);
        budgetsView.setVisibility(state == LoadState.CONTENT ? View.VISIBLE : View.GONE);
    }

    private void loadCategories() {
        viewModel.loadCategories();
    }

    private void updateTotals() {
        long limit = 0L;
        long spent = 0L;
        for (Budget budget : currentBudgets) {
            limit += budget.getAmount();
            spent += spentMap.getOrDefault(budget.getCategoryId(), 0L);
        }
        totalBudget.setText(CurrencyFormatter.format(limit));
        totalSpent.setText(CurrencyFormatter.format(spent));
    }

    public void showAddBudgetDialog() {
        if (categories.isEmpty()) {
            loadCategories();
            Snackbar.make(requireView(), R.string.category_load_failed, Snackbar.LENGTH_LONG).show();
            return;
        }
        View content = LayoutInflater.from(requireContext()).inflate(R.layout.dialog_add_budget, null);
        MaterialAutoCompleteTextView dropdown = content.findViewById(R.id.dropdown_category);
        TextInputEditText amount = content.findViewById(R.id.et_amount);
        TextInputLayout amountLayout = content.findViewById(R.id.layout_amount);
        ArrayAdapter<CategoryDto> categoriesAdapter = new ArrayAdapter<>(requireContext(),
                android.R.layout.simple_dropdown_item_1line, categories);
        dropdown.setAdapter(categoriesAdapter);
        final CategoryDto[] selected = {categories.get(0)};
        dropdown.setText(selected[0].toString(), false);
        dropdown.setOnItemClickListener((parent, view, position, id) -> selected[0] = categories.get(position));
        amount.setKeyListener(android.text.method.DigitsKeyListener.getInstance("0123456789.,"));
        amount.addTextChangedListener(new NumberTextWatcher(amount));

        AlertDialog dialog = new MaterialAlertDialogBuilder(requireContext())
                .setTitle(R.string.add_budget)
                .setView(content)
                .setPositiveButton(R.string.save, null)
                .setNegativeButton(R.string.cancel, null)
                .create();
        dialog.setOnShowListener(ignored -> dialog.getButton(AlertDialog.BUTTON_POSITIVE)
                .setOnClickListener(v -> {
                    String raw = amount.getText() == null ? ""
                            : amount.getText().toString().replace(".", "").replace(",", "").trim();
                    long value;
                    try { value = Long.parseLong(raw); }
                    catch (RuntimeException exception) {
                        amountLayout.setError(getString(R.string.please_enter_amount));
                        return;
                    }
                    if (value <= 0) {
                        amountLayout.setError(getString(R.string.amount_must_be_positive));
                        return;
                    }
                    int[] month = viewModel.getSelectedMonthYear().getValue();
                    if (month == null) return;
                    String key = String.format("%04d-%02d", month[0], month[1] + 1);
                    Budget budget = new Budget(selected[0].id.hashCode(), value, key, viewModel.getUserId());
                    budget.setRemoteCategoryId(selected[0].id);
                    budget.setRemoteCategoryName(selected[0].name);
                    budget.setRemoteCategoryColor(selected[0].color);
                    budget.setRemoteCategoryIcon(selected[0].icon);
                    viewModel.insertBudget(budget);
                    dialog.dismiss();
                }));
        dialog.show();
    }

    private void showEditBudgetDialog(Budget budget) {
        View content = LayoutInflater.from(requireContext()).inflate(R.layout.dialog_add_budget, null);
        MaterialAutoCompleteTextView dropdown = content.findViewById(R.id.dropdown_category);
        TextInputEditText amount = content.findViewById(R.id.et_amount);
        TextInputLayout amountLayout = content.findViewById(R.id.layout_amount);
        dropdown.setText(budget.getRemoteCategoryName(), false);
        dropdown.setEnabled(false);
        amount.setText(String.valueOf(budget.getAmount()));
        amount.addTextChangedListener(new NumberTextWatcher(amount));
        AlertDialog dialog = new MaterialAlertDialogBuilder(requireContext())
                .setTitle(R.string.edit)
                .setView(content)
                .setPositiveButton(R.string.save, null)
                .setNegativeButton(R.string.cancel, null)
                .create();
        dialog.setOnShowListener(ignored -> dialog.getButton(AlertDialog.BUTTON_POSITIVE)
                .setOnClickListener(v -> {
                    String raw = amount.getText() == null ? "" : amount.getText().toString()
                            .replace(".", "").replace(",", "").trim();
                    long value;
                    try { value = Long.parseLong(raw); }
                    catch (RuntimeException exception) { value = 0L; }
                    if (value <= 0) {
                        amountLayout.setError(getString(R.string.amount_must_be_positive));
                        return;
                    }
                    viewModel.updateBudget(budget, value);
                    dialog.dismiss();
                }));
        dialog.show();
    }

    private void confirmDelete(Budget budget) {
        new MaterialAlertDialogBuilder(requireContext())
                .setTitle(R.string.delete)
                .setMessage(R.string.confirm_delete)
                .setPositiveButton(R.string.delete, (dialog, which) -> viewModel.deleteBudget(budget))
                .setNegativeButton(R.string.cancel, null)
                .show();
    }
}
