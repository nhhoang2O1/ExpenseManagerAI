package com.example.appquanlychitieu.ui.common;

import android.content.Intent;
import android.view.LayoutInflater;
import android.view.View;
import android.widget.TextView;

import androidx.appcompat.app.AppCompatActivity;
import androidx.core.content.ContextCompat;

import com.example.appquanlychitieu.MainActivity;
import com.example.appquanlychitieu.R;
import com.example.appquanlychitieu.data.remote.dto.BudgetAlertDto;
import com.example.appquanlychitieu.util.CurrencyFormatter;
import com.google.android.material.dialog.MaterialAlertDialogBuilder;
import com.google.android.material.progressindicator.LinearProgressIndicator;

public final class BudgetAlertDialog {
    private BudgetAlertDialog() { }

    public static boolean showIfPresent(
            AppCompatActivity activity,
            BudgetAlertDto alert,
            Runnable closeCurrentScreen) {
        if (alert == null || alert.level == null) return false;
        boolean exceeded = "EXCEEDED".equalsIgnoreCase(alert.level);
        boolean reached = exceeded && alert.exceededAmount <= 0;
        int title = reached ? R.string.budget_alert_reached_title
                : exceeded ? R.string.budget_alert_exceeded_title
                : R.string.budget_alert_approaching_title;
        View content = LayoutInflater.from(activity).inflate(R.layout.dialog_budget_alert, null);
        TextView category = content.findViewById(R.id.tv_budget_alert_category);
        TextView spent = content.findViewById(R.id.tv_budget_alert_spent);
        TextView limit = content.findViewById(R.id.tv_budget_alert_limit);
        TextView detail = content.findViewById(R.id.tv_budget_alert_detail);
        LinearProgressIndicator progress = content.findViewById(R.id.progress_budget_alert);

        category.setText(activity.getString(
                R.string.budget_alert_category,
                alert.categoryName == null ? "" : alert.categoryName));
        spent.setText(activity.getString(
                R.string.budget_alert_spent, CurrencyFormatter.format(alert.spentAmount)));
        limit.setText(activity.getString(
                R.string.budget_alert_limit, CurrencyFormatter.format(alert.budgetAmount)));
        detail.setText(activity.getString(
                reached ? R.string.budget_alert_reached_detail
                        : exceeded
                        ? R.string.budget_alert_exceeded_detail
                        : R.string.budget_alert_approaching_detail,
                alert.usagePercent,
                CurrencyFormatter.format(
                        exceeded && !reached ? alert.exceededAmount : alert.remainingAmount)));
        progress.setProgressCompat(Math.min(100, Math.max(0, alert.usagePercent)), false);
        int stateColor = ContextCompat.getColor(
                activity, exceeded ? R.color.expense_color : R.color.warning_color);
        progress.setIndicatorColor(stateColor);
        detail.setTextColor(stateColor);

        new MaterialAlertDialogBuilder(activity)
                .setTitle(title)
                .setView(content)
                .setNegativeButton(R.string.understood, (dialog, which) ->
                        closeCurrentScreen.run())
                .setPositiveButton(R.string.view_budget, (dialog, which) -> {
                    Intent intent = new Intent(activity, MainActivity.class);
                    intent.putExtra(MainActivity.EXTRA_OPEN_BUDGET, true);
                    intent.addFlags(Intent.FLAG_ACTIVITY_CLEAR_TOP | Intent.FLAG_ACTIVITY_SINGLE_TOP);
                    activity.startActivity(intent);
                    activity.finish();
                })
                .setOnCancelListener(dialog -> closeCurrentScreen.run())
                .show();
        return true;
    }
}
