package com.example.appquanlychitieu.ui.statistics;

import android.graphics.Color;
import android.graphics.drawable.GradientDrawable;
import android.os.Bundle;
import android.view.LayoutInflater;
import android.view.View;
import android.view.ViewGroup;
import android.widget.ImageView;
import android.widget.LinearLayout;
import android.widget.TextView;

import androidx.annotation.NonNull;
import androidx.annotation.Nullable;
import androidx.fragment.app.Fragment;
import androidx.lifecycle.ViewModelProvider;

import com.example.appquanlychitieu.R;
import com.example.appquanlychitieu.data.model.CategorySummary;
import com.example.appquanlychitieu.data.model.MonthlySummary;
import com.example.appquanlychitieu.ui.common.CategoryVisualResolver;
import com.example.appquanlychitieu.ui.common.LoadState;
import com.example.appquanlychitieu.util.CurrencyFormatter;
import com.example.appquanlychitieu.util.DateUtils;
import com.example.appquanlychitieu.util.FinancialCycleUtils;
import com.example.appquanlychitieu.util.SessionManager;
import com.github.mikephil.charting.charts.PieChart;
import com.github.mikephil.charting.data.PieData;
import com.github.mikephil.charting.data.PieDataSet;
import com.github.mikephil.charting.data.PieEntry;
import com.github.mikephil.charting.formatter.PercentFormatter;
import com.google.android.material.snackbar.Snackbar;

import java.util.ArrayList;
import java.util.Calendar;
import java.util.List;
import java.util.Locale;
import java.util.HashSet;
import java.util.Set;

public class StatisticsFragment extends Fragment {
    private StatisticsViewModel viewModel;
    private PieChart chart;
    private TextView currentMonth;
    private TextView income;
    private TextView expense;
    private TextView savings;
    private TextView balance;
    private TextView categoryEmpty;
    private TextView historyEmpty;
    private LinearLayout categoryList;
    private LinearLayout historyList;
    private View loading;
    private View errorState;
    private int selectedYear;
    private int selectedMonth;

    @Nullable
    @Override
    public View onCreateView(@NonNull LayoutInflater inflater, @Nullable ViewGroup container,
                             @Nullable Bundle savedInstanceState) {
        return inflater.inflate(R.layout.fragment_statistics, container, false);
    }

    @Override
    public void onViewCreated(@NonNull View view, @Nullable Bundle savedInstanceState) {
        super.onViewCreated(view, savedInstanceState);
        chart = view.findViewById(R.id.pie_chart);
        currentMonth = view.findViewById(R.id.tv_current_month);
        income = view.findViewById(R.id.tv_kpi_income);
        expense = view.findViewById(R.id.tv_kpi_expense);
        savings = view.findViewById(R.id.tv_kpi_savings);
        balance = view.findViewById(R.id.tv_kpi_balance);
        categoryEmpty = view.findViewById(R.id.tv_empty);
        historyEmpty = view.findViewById(R.id.tv_history_empty);
        categoryList = view.findViewById(R.id.layout_category_summary);
        historyList = view.findViewById(R.id.layout_monthly_history);
        loading = view.findViewById(R.id.progress_loading);
        errorState = view.findViewById(R.id.layout_error_state);
        setupChart();

        viewModel = new ViewModelProvider(this).get(StatisticsViewModel.class);
        view.findViewById(R.id.btn_prev_month).setOnClickListener(v -> viewModel.previousMonth());
        view.findViewById(R.id.btn_next_month).setOnClickListener(v -> viewModel.nextMonth());
        view.findViewById(R.id.btn_retry).setOnClickListener(v -> viewModel.refreshRemoteStatistics());
        viewModel.getSelectedMonthYear().observe(getViewLifecycleOwner(), month -> {
            selectedYear = month[0];
            selectedMonth = month[1];
            currentMonth.setText(DateUtils.formatDisplayMonth(DateUtils.getStartOfMonth(month[0], month[1])));
            updateKpis(viewModel.getMonthlySummary().getValue());
        });
        viewModel.getCategorySummary().observe(getViewLifecycleOwner(), this::renderCategories);
        viewModel.getMonthlySummary().observe(getViewLifecycleOwner(), summaries -> {
            renderHistory(summaries);
            updateKpis(summaries);
        });
        viewModel.getLoadState().observe(getViewLifecycleOwner(), this::renderState);
        viewModel.getRemoteError().observe(getViewLifecycleOwner(), message -> {
            if (message != null && !message.trim().isEmpty())
                Snackbar.make(view, message, Snackbar.LENGTH_LONG).show();
        });
    }

    private void setupChart() {
        chart.setUsePercentValues(true);
        chart.getDescription().setEnabled(false);
        chart.getLegend().setEnabled(false);
        chart.setDrawEntryLabels(false);
        chart.setRotationEnabled(false);
        chart.setHoleRadius(58f);
        chart.setTransparentCircleRadius(62f);
        chart.setHoleColor(requireContext().getColor(R.color.surface));
        chart.setCenterText(getString(R.string.expense));
        chart.setCenterTextColor(requireContext().getColor(R.color.text_secondary));
        chart.setCenterTextSize(12f);
    }

    private void renderCategories(List<CategorySummary> summaries) {
        boolean empty = summaries == null || summaries.isEmpty();
        chart.setVisibility(empty ? View.GONE : View.VISIBLE);
        categoryList.setVisibility(empty ? View.GONE : View.VISIBLE);
        categoryEmpty.setVisibility(empty ? View.VISIBLE : View.GONE);
        categoryList.removeAllViews();
        if (empty) return;

        List<PieEntry> entries = new ArrayList<>();
        List<Integer> colors = new ArrayList<>();
        Set<Integer> customColors = new HashSet<>();
        for (CategorySummary summary : summaries) {
            entries.add(new PieEntry((float) summary.getTotalAmount(), summary.getCategoryName()));
            int color = CategoryVisualResolver.isDefaultCategoryName(summary.getCategoryName())
                    ? CategoryVisualResolver.resolveChartColor(
                    String.valueOf(summary.getCategoryId()), summary.getCategoryColor())
                    : CategoryVisualResolver.resolveCustomChartColor(
                    String.valueOf(summary.getCategoryId()), customColors);
            colors.add(color);
            View item = LayoutInflater.from(requireContext()).inflate(
                    R.layout.item_category_summary, categoryList, false);
            ((TextView) item.findViewById(R.id.tv_category_name)).setText(summary.getCategoryName());
            int count = summary.getTransactionCount();
            ((TextView) item.findViewById(R.id.tv_transaction_count)).setText(
                    getResources().getQuantityString(R.plurals.transactions_count, count, count));
            ((TextView) item.findViewById(R.id.tv_amount)).setText(
                    CurrencyFormatter.format(summary.getTotalAmount()));
            View dot = item.findViewById(R.id.view_color);
            GradientDrawable background = new GradientDrawable();
            background.setShape(GradientDrawable.OVAL);
            background.setColor(color);
            dot.setBackground(background);
            categoryList.addView(item);
        }
        PieDataSet set = new PieDataSet(entries, "");
        set.setColors(colors);
        set.setSliceSpace(2f);
        set.setValueTextColor(Color.WHITE);
        set.setValueTextSize(10f);
        set.setValueFormatter(new PercentFormatter(chart));
        chart.setData(new PieData(set));
        chart.invalidate();
    }

    private void renderHistory(List<MonthlySummary> summaries) {
        boolean empty = summaries == null || summaries.isEmpty();
        historyList.setVisibility(empty ? View.GONE : View.VISIBLE);
        historyEmpty.setVisibility(empty ? View.VISIBLE : View.GONE);
        historyList.removeAllViews();
        if (empty) return;
        for (MonthlySummary summary : summaries) {
            View item = LayoutInflater.from(requireContext()).inflate(
                    R.layout.item_monthly_summary, historyList, false);
            TextView month = item.findViewById(R.id.tv_month_year);
            try {
                String[] parts = summary.getMonthYear().split("-");
                Calendar calendar = Calendar.getInstance();
                calendar.set(Integer.parseInt(parts[0]), Integer.parseInt(parts[1]) - 1, 1);
                month.setText(DateUtils.formatDisplayMonth(calendar.getTimeInMillis()));
            } catch (RuntimeException ignored) { month.setText(summary.getMonthYear()); }
            ((TextView) item.findViewById(R.id.tv_income)).setText(getString(
                    R.string.positive_amount, CurrencyFormatter.format(summary.getTotalIncome())));
            ((TextView) item.findViewById(R.id.tv_expense)).setText(getString(
                    R.string.negative_amount, CurrencyFormatter.format(summary.getTotalExpense())));
            TextView itemBalance = item.findViewById(R.id.tv_balance);
            itemBalance.setText(CurrencyFormatter.format(summary.getBalance()));
            itemBalance.setTextColor(requireContext().getColor(summary.getBalance() >= 0
                    ? R.color.income_color : R.color.expense_color));
            historyList.addView(item);
        }
    }

    private void updateKpis(List<MonthlySummary> summaries) {
        MonthlySummary selected = null;
        java.time.LocalDate cycleStart = FinancialCycleUtils.cycleStartForMonth(selectedYear, selectedMonth,
                new SessionManager(requireContext()).getFinancialCycleStartDay());
        String key = String.format(Locale.ROOT, "%04d-%02d", cycleStart.getYear(), cycleStart.getMonthValue());
        if (summaries != null) {
            for (MonthlySummary summary : summaries) {
                if (key.equals(summary.getMonthYear())) { selected = summary; break; }
            }
        }
        long in = selected == null ? 0L : selected.getTotalIncome();
        long out = selected == null ? 0L : selected.getTotalExpense();
        long saved = selected == null ? 0L : selected.getTotalSavings();
        income.setText(CurrencyFormatter.format(in));
        expense.setText(CurrencyFormatter.format(out));
        savings.setText(CurrencyFormatter.format(saved));
        balance.setText(CurrencyFormatter.format(in - out - saved));
    }

    private void renderState(LoadState state) {
        loading.setVisibility(state == LoadState.LOADING ? View.VISIBLE : View.GONE);
        errorState.setVisibility(state == LoadState.ERROR ? View.VISIBLE : View.GONE);
    }

    @Override
    public void onResume() {
        super.onResume();
        if (viewModel != null) viewModel.refreshRemoteStatistics();
    }
}
