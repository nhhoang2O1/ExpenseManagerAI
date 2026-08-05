package com.example.appquanlychitieu.ui.goals;

import android.content.Context;
import android.view.LayoutInflater;
import android.view.View;
import android.view.ViewGroup;
import android.widget.TextView;
import android.widget.ArrayAdapter;
import androidx.annotation.NonNull;
import androidx.annotation.Nullable;
import com.example.appquanlychitieu.R;
import com.example.appquanlychitieu.data.model.GoalHistory;
import com.example.appquanlychitieu.util.CurrencyFormatter;
import com.example.appquanlychitieu.util.DateUtils;
import java.util.ArrayList;
import java.util.List;

public class GoalHistoryAdapter extends ArrayAdapter<GoalHistory> {

    private final Context context;
    private List<GoalHistory> historyList = new ArrayList<>();

    public GoalHistoryAdapter(Context context) {
        super(context, R.layout.item_goal_history, new ArrayList<>());
        this.context = context;
    }

    public void setHistoryList(List<GoalHistory> historyList) {
        this.historyList = historyList;
        clear();
        addAll(historyList);
        notifyDataSetChanged();
    }

    @NonNull
    @Override
    public View getView(int position, @Nullable View convertView, @NonNull ViewGroup parent) {
        if (convertView == null) {
            convertView = LayoutInflater.from(context).inflate(R.layout.item_goal_history, parent, false);
        }

        GoalHistory history = getItem(position);
        
        TextView tvDate = convertView.findViewById(R.id.tv_date);
        TextView tvAmount = convertView.findViewById(R.id.tv_amount);

        if (history != null) {
            tvDate.setText(DateUtils.formatDate(history.getDate()));
            tvAmount.setText(context.getString(R.string.positive_amount,
                    CurrencyFormatter.format(history.getAmountAdded())));
        }

        return convertView;
    }
}
