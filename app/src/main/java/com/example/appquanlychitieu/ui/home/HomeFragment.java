package com.example.appquanlychitieu.ui.home;

import android.content.Intent;
import android.os.Bundle;
import android.view.LayoutInflater;
import android.view.View;
import android.view.ViewGroup;
import android.widget.TextView;

import androidx.annotation.NonNull;
import androidx.annotation.Nullable;
import androidx.fragment.app.Fragment;
import androidx.lifecycle.ViewModelProvider;
import androidx.recyclerview.widget.LinearLayoutManager;
import androidx.recyclerview.widget.RecyclerView;

import com.example.appquanlychitieu.R;
import com.example.appquanlychitieu.data.model.Transaction;
import com.example.appquanlychitieu.data.model.TransactionType;
import com.example.appquanlychitieu.ui.common.LoadState;
import com.example.appquanlychitieu.ui.receipt.ReceiptScanActivity;
import com.example.appquanlychitieu.ui.reminder.ReminderActivity;
import com.example.appquanlychitieu.ui.transaction.AddEditTransactionActivity;
import com.example.appquanlychitieu.ui.transaction.OcrReceiptDetails;
import com.example.appquanlychitieu.ui.transaction.TransactionListAdapter;
import com.example.appquanlychitieu.util.CurrencyFormatter;
import com.example.appquanlychitieu.util.DateUtils;
import com.example.appquanlychitieu.util.SessionManager;
import com.google.android.material.bottomnavigation.BottomNavigationView;
import com.google.android.material.datepicker.MaterialDatePicker;
import com.google.android.material.dialog.MaterialAlertDialogBuilder;
import com.google.android.material.snackbar.Snackbar;

import java.time.Instant;
import java.time.ZoneId;

public class HomeFragment extends Fragment {
    private HomeViewModel viewModel;
    private TransactionListAdapter adapter;
    private RecyclerView recentView;
    private View emptyState;
    private View loading;
    private View errorState;
    private View syncBanner;
    private TextView balance;
    private TextView income;
    private TextView expense;
    private TextView dailyDate;
    private TextView dailyIncome;
    private TextView dailyExpense;

    @Nullable
    @Override
    public View onCreateView(@NonNull LayoutInflater inflater, @Nullable ViewGroup container,
                             @Nullable Bundle savedInstanceState) {
        return inflater.inflate(R.layout.fragment_home, container, false);
    }

    @Override
    public void onViewCreated(@NonNull View view, @Nullable Bundle savedInstanceState) {
        super.onViewCreated(view, savedInstanceState);
        SessionManager session = new SessionManager(requireContext());
        ((TextView) view.findViewById(R.id.tv_greeting)).setText(getString(
                R.string.hello_user, session.getUserName()));
        ((TextView) view.findViewById(R.id.tv_month_year)).setText(getString(
                R.string.today_format, DateUtils.formatDate(System.currentTimeMillis())));

        balance = view.findViewById(R.id.tv_balance);
        income = view.findViewById(R.id.tv_income);
        expense = view.findViewById(R.id.tv_expense);
        dailyDate = view.findViewById(R.id.tv_daily_date);
        dailyIncome = view.findViewById(R.id.tv_daily_income);
        dailyExpense = view.findViewById(R.id.tv_daily_expense);
        recentView = view.findViewById(R.id.rv_recent_transactions);
        emptyState = view.findViewById(R.id.layout_empty_state);
        loading = view.findViewById(R.id.progress_loading);
        errorState = view.findViewById(R.id.layout_error_state);
        syncBanner = view.findViewById(R.id.layout_sync_banner);

        adapter = new TransactionListAdapter(requireContext());
        recentView.setLayoutManager(new LinearLayoutManager(requireContext()));
        recentView.setNestedScrollingEnabled(false);
        recentView.setAdapter(adapter);
        viewModel = new ViewModelProvider(this).get(HomeViewModel.class);

        view.findViewById(R.id.btn_quick_income).setOnClickListener(v -> openAdd(TransactionType.INCOME));
        view.findViewById(R.id.btn_quick_expense).setOnClickListener(v -> openAdd(TransactionType.EXPENSE));
        view.findViewById(R.id.btn_quick_scan).setOnClickListener(v ->
                startActivity(new Intent(requireContext(), ReceiptScanActivity.class)));
        view.findViewById(R.id.btn_quick_reminder).setOnClickListener(v ->
                startActivity(new Intent(requireContext(), ReminderActivity.class)));
        view.findViewById(R.id.btn_empty_cta).setOnClickListener(v -> openAdd(TransactionType.EXPENSE));
        view.findViewById(R.id.btn_retry).setOnClickListener(v -> viewModel.refreshRemoteTransactions());
        view.findViewById(R.id.btn_banner_retry).setOnClickListener(v -> viewModel.refreshRemoteTransactions());
        view.findViewById(R.id.tv_see_all).setOnClickListener(v -> {
            BottomNavigationView nav = requireActivity().findViewById(R.id.bottom_navigation);
            if (nav != null) nav.setSelectedItemId(R.id.navigation_transactions);
        });
        view.findViewById(R.id.card_daily_stats).setOnClickListener(v -> showDatePicker());

        adapter.setOnItemClickListener(new TransactionListAdapter.OnItemClickListener() {
            @Override public void onClick(Transaction transaction) { openTransaction(transaction); }
            @Override public void onLongClick(Transaction transaction) { confirmDelete(transaction); }
        });
        viewModel.getTotalIncome().observe(getViewLifecycleOwner(), value ->
                income.setText(CurrencyFormatter.format(value == null ? 0L : value)));
        viewModel.getTotalExpense().observe(getViewLifecycleOwner(), value ->
                expense.setText(CurrencyFormatter.format(value == null ? 0L : value)));
        viewModel.getBalance().observe(getViewLifecycleOwner(), value ->
                balance.setText(CurrencyFormatter.format(value == null ? 0L : value)));
        viewModel.getDailyIncome().observe(getViewLifecycleOwner(), value ->
                dailyIncome.setText(getString(R.string.positive_amount,
                        CurrencyFormatter.format(value == null ? 0L : value))));
        viewModel.getDailyExpense().observe(getViewLifecycleOwner(), value ->
                dailyExpense.setText(getString(R.string.negative_amount,
                        CurrencyFormatter.format(value == null ? 0L : value))));
        viewModel.getSelectedDate().observe(getViewLifecycleOwner(), value ->
                dailyDate.setText(DateUtils.formatDate(value == null ? System.currentTimeMillis() : value)));
        viewModel.getRecentTransactions().observe(getViewLifecycleOwner(), adapter::setTransactions);
        viewModel.getLoadState().observe(getViewLifecycleOwner(), this::renderState);
        viewModel.getRemoteError().observe(getViewLifecycleOwner(), message -> {
            syncBanner.setVisibility(message != null && !message.trim().isEmpty()
                    && !adapter.getCurrentList().isEmpty() ? View.VISIBLE : View.GONE);
        });
    }

    private void renderState(LoadState state) {
        loading.setVisibility(state == LoadState.LOADING ? View.VISIBLE : View.GONE);
        errorState.setVisibility(state == LoadState.ERROR ? View.VISIBLE : View.GONE);
        emptyState.setVisibility(state == LoadState.EMPTY ? View.VISIBLE : View.GONE);
        recentView.setVisibility(state == LoadState.CONTENT ? View.VISIBLE : View.GONE);
    }

    private void showDatePicker() {
        Long selected = viewModel.getSelectedDate().getValue();
        MaterialDatePicker<Long> picker = MaterialDatePicker.Builder.datePicker()
                .setTitleText(R.string.daily_overview)
                .setSelection(selected == null ? System.currentTimeMillis() : selected)
                .build();
        picker.addOnPositiveButtonClickListener(viewModel::setSelectedDate);
        picker.show(getParentFragmentManager(), "home_daily_date");
    }

    private void openAdd(TransactionType type) {
        Intent intent = new Intent(requireContext(), AddEditTransactionActivity.class);
        intent.putExtra(AddEditTransactionActivity.EXTRA_TRANSACTION_TYPE, type.name());
        startActivity(intent);
    }

    private void openTransaction(Transaction transaction) {
        OcrReceiptDetails.show(requireContext(), transaction, () -> editTransaction(transaction));
    }

    private void editTransaction(Transaction transaction) {
        Intent intent = new Intent(requireContext(), AddEditTransactionActivity.class);
        intent.putExtra(AddEditTransactionActivity.EXTRA_REMOTE_TRANSACTION_ID, transaction.getRemoteId());
        intent.putExtra(AddEditTransactionActivity.EXTRA_REMOTE_CATEGORY_ID, transaction.getRemoteCategoryId());
        intent.putExtra(AddEditTransactionActivity.EXTRA_REMOTE_STORE_NAME, transaction.getRemoteStoreName());
        intent.putExtra(AddEditTransactionActivity.EXTRA_VERSION, transaction.getVersion());
        intent.putExtra(AddEditTransactionActivity.EXTRA_PREFILL_AMOUNT,
                String.valueOf((long) transaction.getAmount()));
        intent.putExtra(AddEditTransactionActivity.EXTRA_PREFILL_NOTE, transaction.getNote());
        intent.putExtra(AddEditTransactionActivity.EXTRA_PREFILL_DATE,
                Instant.ofEpochMilli(transaction.getDate()).atZone(ZoneId.of("Asia/Ho_Chi_Minh"))
                        .toLocalDate().toString());
        intent.putExtra(AddEditTransactionActivity.EXTRA_TRANSACTION_TYPE, transaction.getType().name());
        startActivity(intent);
    }

    private void confirmDelete(Transaction transaction) {
        new MaterialAlertDialogBuilder(requireContext())
                .setTitle(R.string.confirm_delete_title)
                .setMessage(R.string.confirm_delete)
                .setPositiveButton(R.string.delete, (dialog, which) -> viewModel.deleteTransaction(transaction))
                .setNegativeButton(R.string.cancel, null)
                .show();
    }

    @Override
    public void onResume() {
        super.onResume();
        if (viewModel != null) viewModel.refreshRemoteTransactions();
    }
}
